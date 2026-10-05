using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;

namespace AcaiPos.Desktop.Printing;

public sealed class WpfTicketPrinter : ITicketPrinter
{
    private readonly ITicketFormatter _formatter;

    public WpfTicketPrinter(ITicketFormatter formatter)
    {
        _formatter = formatter;
    }

    public Task PrintAsync(TicketDto ticket, CancellationToken cancellationToken = default)
    {
        var text = _formatter.Format(ticket);
        var dispatcher = System.Windows.Application.Current.Dispatcher;

        return dispatcher.InvokeAsync(() =>
        {
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                PagePadding = new Thickness(20),
                ColumnWidth = double.PositiveInfinity
            };

            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                document.Blocks.Add(new Paragraph(new Run(line))
                {
                    Margin = new Thickness(0),
                    LineHeight = 16
                });
            }

            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
                return;

            document.PageWidth = dialog.PrintableAreaWidth;
            document.PageHeight = dialog.PrintableAreaHeight;
            IDocumentPaginatorSource source = document;
            dialog.PrintDocument(source.DocumentPaginator, $"Ticket {ticket.TicketNumber}");
        }).Task;
    }
}
