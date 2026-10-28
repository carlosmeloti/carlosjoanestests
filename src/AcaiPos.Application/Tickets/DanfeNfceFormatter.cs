using System.Globalization;
using System.Text;
using AcaiPos.Application.Models;

namespace AcaiPos.Application.Tickets;

public static class DanfeNfceFormatter
{
    public static string Format(TicketDto ticket)
    {
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        var sb = new StringBuilder();
        var emitidoEm = ticket.SoldAt.Kind == DateTimeKind.Utc ? ticket.SoldAt.ToLocalTime() : ticket.SoldAt;

        sb.AppendLine(Center(ticket.DocumentoEmitente ?? ticket.CompanyName, 42));
        if (!string.IsNullOrWhiteSpace(ticket.CnpjEmitente))
            sb.AppendLine(Center(FormatarCnpj(ticket.CnpjEmitente), 42));
        sb.AppendLine(Center($"IE {ticket.InscricaoEstadual}", 42));
        if (!string.IsNullOrWhiteSpace(ticket.EnderecoEmitente))
            sb.AppendLine(Center(ticket.EnderecoEmitente, 42));
        sb.AppendLine(new string('-', 42));
        sb.AppendLine(Center("DANFE NFC-e", 42));
        sb.AppendLine(Center("Documento Auxiliar da Nota", 42));
        sb.AppendLine(Center("Fiscal de Consumidor Eletronica", 42));
        sb.AppendLine(new string('-', 42));

        if (ticket.IsReprint)
            sb.AppendLine(Center("*** REIMPRESSAO ***", 42));
        if (ticket.Homologacao)
            sb.AppendLine(Center("EMITIDA EM HOMOLOGACAO", 42));
        if (ticket.Contingencia)
        {
            sb.AppendLine(Center("EMITIDA EM CONTINGENCIA", 42));
            sb.AppendLine(Center("Pendente de autorizacao", 42));
        }

        if (ticket.Status == Domain.Enums.SaleStatus.Cancelled)
            sb.AppendLine(Center("*** CANCELADA ***", 42));

        foreach (var item in ticket.Items)
        {
            sb.AppendLine(item.ProductName);
            sb.AppendLine($"  {item.Quantity.ToString("0.###", cultura)} x {item.UnitPrice.ToString("C", cultura)}");
            sb.AppendLine(Right(item.Subtotal.ToString("C", cultura), 42));
        }

        sb.AppendLine(new string('-', 42));
        sb.AppendLine(Pair("Subtotal", ticket.Subtotal, cultura));
        if (ticket.DiscountAmount > 0)
            sb.AppendLine(Pair("Desconto", ticket.DiscountAmount, cultura));
        if (ticket.SurchargeAmount > 0)
            sb.AppendLine(Pair("Acrescimo", ticket.SurchargeAmount, cultura));
        sb.AppendLine(Pair("TOTAL", ticket.Total, cultura));
        sb.AppendLine(new string('-', 42));

        foreach (var payment in ticket.Payments)
            sb.AppendLine(Pair(payment.MethodLabel, payment.Amount, cultura));
        if (ticket.ChangeAmount > 0)
            sb.AppendLine(Pair("Troco", ticket.ChangeAmount, cultura));

        sb.AppendLine(new string('-', 42));
        sb.AppendLine(Center("Tributos Totais Incidentes", 42));
        if (ticket.TributoIbs is decimal ibs && ticket.TributoCbs is decimal cbs)
            sb.AppendLine(Pair("(Lei Federal 12.741/2012)", ibs + cbs, cultura));
        else
            sb.AppendLine(Center("(Lei Federal 12.741/2012)", 42));
        if (ticket.TributoIbs is decimal valorIbs)
            sb.AppendLine(Pair("IBS", valorIbs, cultura));
        if (ticket.TributoCbs is decimal valorCbs)
            sb.AppendLine(Pair("CBS", valorCbs, cultura));
        sb.AppendLine(new string('-', 42));
        sb.AppendLine(Center("Consulte pela Chave de Acesso em", 42));
        if (!string.IsNullOrWhiteSpace(ticket.UrlConsulta))
            sb.AppendLine(Center(ticket.UrlConsulta, 42));
        sb.AppendLine(FormatarChave(ticket.ChaveAcesso));
        sb.AppendLine($"NFC-e n {ticket.NumeroNfce:000000000}  Serie {ticket.SerieNfce:000}");
        sb.AppendLine(emitidoEm.ToString("dd/MM/yyyy HH:mm:ss", cultura));
        if (ticket.Contingencia)
            sb.AppendLine("Protocolo: pendente");
        else if (!string.IsNullOrWhiteSpace(ticket.Protocolo))
            sb.AppendLine($"Protocolo: {ticket.Protocolo}");

        sb.AppendLine(new string('-', 42));
        sb.AppendLine(string.IsNullOrWhiteSpace(ticket.CpfConsumidor)
            ? "CONSUMIDOR NAO IDENTIFICADO"
            : $"CONSUMIDOR CPF {FormatarCpf(ticket.CpfConsumidor)}");
        sb.AppendLine(Center("Obrigado pela preferencia!", 42));
        return sb.ToString();
    }

    private static string FormatarChave(string? chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
            return string.Empty;

        var digitos = new string(chave.Where(char.IsDigit).ToArray());
        var linhas = new StringBuilder();
        for (var i = 0; i < digitos.Length; i += 4)
        {
            if (i > 0 && i % 24 == 0)
                linhas.AppendLine();
            else if (i > 0)
                linhas.Append(' ');

            var tamanho = Math.Min(4, digitos.Length - i);
            linhas.Append(digitos.Substring(i, tamanho));
        }

        return Center(linhas.ToString(), 42);
    }

    private static string FormatarCnpj(string? cnpj)
    {
        var digitos = new string((cnpj ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length != 14)
            return cnpj ?? string.Empty;

        return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..]}";
    }

    private static string FormatarCpf(string cpf)
        => cpf.Length == 11 ? $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}" : cpf;

    private static string Center(string text, int width)
    {
        var valor = text ?? string.Empty;
        if (valor.Length >= width)
            return valor;

        var pad = (width - valor.Length) / 2;
        return valor.PadLeft(pad + valor.Length).PadRight(width);
    }

    private static string Right(string text, int width)
        => text.Length >= width ? text : text.PadLeft(width);

    private static string Pair(string label, decimal value, CultureInfo cultura)
    {
        var amount = value.ToString("C", cultura);
        var space = 42 - label.Length - amount.Length;
        if (space < 1)
            return label + " " + amount;

        return label + new string(' ', space) + amount;
    }
}
