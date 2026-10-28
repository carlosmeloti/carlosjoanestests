using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;

namespace AcaiPos.Desktop.Printing;

/// <summary>
/// Formata o ticket e envia o texto à impressora padrão.
/// </summary>
public sealed class SpoolerTicketPrinter : ITicketPrinter
{
    private readonly ITicketFormatter _formatter;
    private readonly SpoolerTextPrinter _printer;

    public SpoolerTicketPrinter(ITicketFormatter formatter, SpoolerTextPrinter printer)
    {
        _formatter = formatter;
        _printer = printer;
    }

    public Task PrintAsync(TicketDto ticket, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = _formatter.Format(ticket);
        var jobName = $"Ticket {ticket.TicketNumber}";
        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("A impressão precisa da interface do PDV.");

        return dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var qr = string.IsNullOrWhiteSpace(ticket.QrCode) ? null : QrCodeImagem.ParaImpressao(ticket.QrCode);
            _printer.Print(jobName, text, qr);
        }).Task;
    }
}
