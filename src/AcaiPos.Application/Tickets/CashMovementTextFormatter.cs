using System.Globalization;
using System.Text;
using AcaiPos.Application.Models;

namespace AcaiPos.Application.Tickets;

/// <summary>
/// Dados do ciclo de caixa prontos para impressão, com a loja e o instante da impressão.
/// </summary>
public sealed record CashMovementReport(
    string StoreCode,
    string StoreName,
    CashSessionDetailDto Detail,
    DateTime PrintedAt);

/// <summary>
/// Relatório de movimento de caixa em 42 colunas, no mesmo estilo do cupom térmico.
/// </summary>
public sealed class CashMovementTextFormatter
{
    public const int Width = 42;
    public const string CashBalanceFormula = "abertura + dinheiro + suprimentos \u2212 sangrias";
    public const string GeneralBalanceFormula = "abertura + vendas + suprimentos \u2212 sangrias";
    public const string NoWithdrawalsText = "Nenhuma sangria neste ciclo.";
    public const string NoSuppliesText = "Nenhum suprimento neste ciclo.";
    public const string StillOpenText = "em aberto";

    /// <summary>
    /// Monta o texto da bobina: cabeçalho, saldos, sangrias, suprimentos e formas de pagamento.
    /// </summary>
    public string Format(CashMovementReport report)
    {
        var detail = report.Detail;
        var sb = new StringBuilder();

        sb.AppendLine(Center("MOVIMENTO DE CAIXA"));
        if (!string.IsNullOrWhiteSpace(report.StoreName))
            sb.AppendLine(Center(report.StoreName.Trim()));

        var place = string.IsNullOrWhiteSpace(report.StoreCode)
            ? $"Terminal {detail.TerminalCode}"
            : $"{report.StoreCode.Trim()} · Terminal {detail.TerminalCode}";
        sb.AppendLine(Center(place));
        sb.AppendLine(Rule());

        AppendPair(sb, "Status", StatusLabel(detail));
        AppendPair(sb, "Operador", string.IsNullOrWhiteSpace(detail.OperatorName) ? "—" : detail.OperatorName.Trim());
        AppendPair(sb, "Abertura", FormatDateTime(detail.OpenedAt));
        AppendPair(sb, "Fechamento", ClosingLabel(detail));
        sb.AppendLine(Rule());

        sb.AppendLine(Center("SALDOS"));
        AppendPair(sb, "Fundo de abertura", Money(detail.OpeningAmount));
        AppendPair(sb, "Saldo em dinheiro", Money(detail.CashBalance));
        AppendWrapped(sb, CashBalanceFormula);
        AppendPair(sb, "Saldo geral", Money(detail.GeneralBalance));
        AppendWrapped(sb, GeneralBalanceFormula);
        AppendPair(sb, "Total vendido", Money(detail.TotalSold));
        sb.AppendLine(Rule());

        sb.AppendLine(Center("SANGRIAS"));
        AppendMovements(sb, detail.WithdrawalsList, NoWithdrawalsText);
        AppendPair(sb, "Subtotal sangrias", Money(detail.Withdrawals));
        sb.AppendLine(Rule());

        sb.AppendLine(Center("SUPRIMENTOS"));
        AppendMovements(sb, detail.SuppliesList, NoSuppliesText);
        AppendPair(sb, "Subtotal suprimentos", Money(detail.Supplies));
        sb.AppendLine(Rule());

        sb.AppendLine(Center("FORMAS DE PAGAMENTO"));
        if (detail.PaymentTotals.Count == 0)
            sb.AppendLine(Clip("Nenhuma venda neste ciclo."));
        else
        {
            foreach (var total in detail.PaymentTotals)
                AppendPair(sb, string.IsNullOrWhiteSpace(total.Label) ? "—" : total.Label.Trim(), Money(total.Amount));
        }

        sb.AppendLine(Rule());
        AppendPair(sb, "TOTAL VENDIDO", Money(detail.TotalSold));
        sb.AppendLine(Rule());
        sb.AppendLine(Center($"Impresso em {FormatDateTime(report.PrintedAt)}"));
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Rótulo ABERTO ou FECHADO do ciclo.
    /// </summary>
    public static string StatusLabel(CashSessionDetailDto detail)
        => detail.IsOpen ? "ABERTO" : "FECHADO";

    /// <summary>
    /// Data e hora do fechamento, ou "em aberto" enquanto o ciclo não foi encerrado.
    /// </summary>
    public static string ClosingLabel(CashSessionDetailDto detail)
        => !detail.IsOpen && detail.ClosedAt is DateTime closedAt
            ? FormatDateTime(closedAt)
            : StillOpenText;

    /// <summary>
    /// Data e hora locais no formato do relatório.
    /// </summary>
    public static string FormatDateTime(DateTime value)
        => ToLocal(value).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Hora local da movimentação.
    /// </summary>
    public static string FormatTime(DateTime value)
        => ToLocal(value).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Valor monetário na cultura atual.
    /// </summary>
    public static string Money(decimal value)
        => value.ToString("C", CultureInfo.CurrentCulture);

    /// <summary>
    /// Converte UTC para o horário local. Valores sem fuso permanecem como foram gravados.
    /// </summary>
    public static DateTime ToLocal(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;

    private static void AppendMovements(
        StringBuilder sb,
        IReadOnlyList<CashSessionMovementDto> movements,
        string emptyText)
    {
        if (movements.Count == 0)
        {
            sb.AppendLine(Clip(emptyText));
            return;
        }

        foreach (var movement in movements)
            AppendMovement(sb, movement);
    }

    private static void AppendMovement(StringBuilder sb, CashSessionMovementDto movement)
    {
        var reason = string.IsNullOrWhiteSpace(movement.Reason) ? "—" : movement.Reason.Trim().Replace('\n', ' ');
        var head = $"{FormatTime(movement.OccurredAt)} {reason}";
        var value = Money(movement.Amount);
        var lines = Wrap(head, Width);
        if (lines.Count == 1 && lines[0].Length + 1 + value.Length <= Width)
        {
            AppendPair(sb, lines[0], value);
            return;
        }

        foreach (var line in lines)
            sb.AppendLine(line);
        sb.AppendLine(Right(value));
    }

    private static void AppendPair(StringBuilder sb, string label, string value)
    {
        var safeLabel = string.IsNullOrWhiteSpace(label) ? "—" : label.Trim();
        if (safeLabel.Length + 1 + value.Length <= Width)
        {
            sb.AppendLine(safeLabel + new string(' ', Width - safeLabel.Length - value.Length) + value);
            return;
        }

        AppendWrapped(sb, safeLabel);
        sb.AppendLine(Right(value));
    }

    private static void AppendWrapped(StringBuilder sb, string text)
    {
        foreach (var line in Wrap(text, Width))
            sb.AppendLine(line);
    }

    private static List<string> Wrap(string text, int width)
    {
        var result = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var piece = word;
            while (piece.Length > width)
            {
                if (current.Length > 0)
                {
                    result.Add(current);
                    current = string.Empty;
                }

                result.Add(piece[..width]);
                piece = piece[width..];
            }

            if (current.Length == 0)
                current = piece;
            else if (current.Length + 1 + piece.Length <= width)
                current += " " + piece;
            else
            {
                result.Add(current);
                current = piece;
            }
        }

        if (current.Length > 0)
            result.Add(current);
        if (result.Count == 0)
            result.Add(string.Empty);
        return result;
    }

    private static string Center(string text)
    {
        var clipped = Clip(text);
        if (clipped.Length >= Width)
            return clipped;

        var pad = (Width - clipped.Length) / 2;
        return clipped.PadLeft(pad + clipped.Length).PadRight(Width);
    }

    private static string Right(string text)
    {
        var clipped = Clip(text);
        return clipped.Length >= Width ? clipped : clipped.PadLeft(Width);
    }

    private static string Clip(string text)
        => text.Length <= Width ? text : text[..(Width - 1)] + "…";

    private static string Rule() => new('-', Width);
}
