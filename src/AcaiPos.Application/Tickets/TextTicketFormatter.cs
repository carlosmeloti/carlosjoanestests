using System.Globalization;
using System.Text;
using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Tickets;

public sealed class TextTicketFormatter : ITicketFormatter
{
    public string Format(TicketDto ticket)
    {
        var sb = new StringBuilder();
        var localSoldAt = ticket.SoldAt.Kind == DateTimeKind.Utc
            ? ticket.SoldAt.ToLocalTime()
            : ticket.SoldAt;

        sb.AppendLine(Center(ticket.CompanyName, 42));
        sb.AppendLine(Center($"{ticket.StoreCode} · {ticket.StoreName}", 42));
        sb.AppendLine(Center($"Terminal {ticket.TerminalCode}", 42));
        sb.AppendLine(new string('-', 42));

        if (ticket.IsReprint)
            sb.AppendLine(Center("*** REIMPRESSAO ***", 42));

        if (ticket.Status == SaleStatus.Cancelled)
            sb.AppendLine(Center("*** CANCELADA ***", 42));

        sb.AppendLine($"Ticket : {ticket.TicketNumber}");
        sb.AppendLine($"Data   : {localSoldAt:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Operador: {ticket.OperatorName}");
        sb.AppendLine(new string('-', 42));

        foreach (var item in ticket.Items)
        {
            sb.AppendLine(item.ProductName);
            sb.AppendLine($"  {item.Quantity:0.###} x {item.UnitPrice.ToString("C", CultureInfo.CurrentCulture)}");
            sb.AppendLine(Right($"  {item.Subtotal.ToString("C", CultureInfo.CurrentCulture)}", 42));
        }

        sb.AppendLine(new string('-', 42));
        sb.AppendLine(Pair("Subtotal", ticket.Subtotal));
        if (ticket.DiscountAmount > 0)
            sb.AppendLine(Pair("Desconto", ticket.DiscountAmount));
        sb.AppendLine(Pair("TOTAL", ticket.Total));
        sb.AppendLine(new string('-', 42));

        foreach (var payment in ticket.Payments)
        {
            sb.AppendLine(Pair(payment.MethodLabel, payment.Amount));
            if (payment.Method == PaymentMethod.Cash && payment.AmountReceived.HasValue)
                sb.AppendLine(Pair("  Recebido", payment.AmountReceived.Value));
        }

        if (ticket.ChangeAmount > 0)
            sb.AppendLine(Pair("Troco", ticket.ChangeAmount));

        sb.AppendLine(new string('-', 42));
        sb.AppendLine(Center("Obrigado pela preferencia!", 42));
        sb.AppendLine();

        return sb.ToString();
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width)
            return text;

        var pad = (width - text.Length) / 2;
        return text.PadLeft(pad + text.Length).PadRight(width);
    }

    private static string Right(string text, int width)
        => text.Length >= width ? text : text.PadLeft(width);

    private static string Pair(string label, decimal value)
    {
        var amount = value.ToString("C", CultureInfo.CurrentCulture);
        var spaces = Math.Max(1, 42 - label.Length - amount.Length);
        return label + new string(' ', spaces) + amount;
    }
}
