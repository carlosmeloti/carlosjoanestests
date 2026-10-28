using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.IO;
using AcaiPos.Application.Models;
using AcaiPos.Application.Tickets;

namespace AcaiPos.Desktop.Printing;

/// <summary>
/// Relatório de NFC-e em A4 retrato, no mesmo desenho do movimento de caixa.
/// </summary>
public sealed class NfceRelatorioA4Printer
{
    /// <summary>
    /// Desenha as páginas A4 em memória, sem enviar à impressora.
    /// </summary>
    public IReadOnlyList<byte[]> RenderPreviewPngs(NfceRelatorio report)
    {
        using var document = CreateDocument();
        var controller = new PreviewPrintController { UseAntiAlias = true };
        document.PrintController = controller;
        Attach(document, report);
        document.Print();

        var pages = new List<byte[]>();
        foreach (PreviewPageInfo info in controller.GetPreviewPageInfo())
        {
            if (info.Image is null)
                continue;
            pages.Add(ToPng(info.Image));
        }

        return pages;
    }

    /// <summary>
    /// Sugere papel A4 e abre o diálogo do Windows. Devolve falso se o operador cancelar.
    /// </summary>
    public bool ShowAndPrint(NfceRelatorio report)
    {
        using var document = CreateDocument();
        document.PrintController = new StandardPrintController();

        using var dialog = new System.Windows.Forms.PrintDialog
        {
            Document = document,
            AllowSomePages = false,
            AllowSelection = false,
            AllowPrintToFile = false,
            UseEXDialog = true
        };

        var owner = TryOwner();
        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (result != System.Windows.Forms.DialogResult.OK)
            return false;

        document.DefaultPageSettings.Landscape = false;
        ApplyA4(document);
        Attach(document, report);
        document.Print();
        return true;
    }

    private static PrintDocument CreateDocument()
    {
        var document = new PrintDocument
        {
            DocumentName = "Relatório de NFC-e"
        };
        document.DefaultPageSettings.Landscape = false;
        document.DefaultPageSettings.Margins = new Margins(48, 48, 48, 48);
        ApplyA4(document);
        return document;
    }

    private static void Attach(PrintDocument document, NfceRelatorio report)
    {
        var kit = new DrawKit();
        var cursor = new PageCursor(report, kit);
        document.EndPrint += (_, _) => kit.Dispose();
        document.PrintPage += (_, e) => cursor.Draw(e);
    }

    private static byte[] ToPng(Image image)
    {
        var srcWidth = Math.Max(1, image.Width);
        var srcHeight = Math.Max(1, image.Height);
        const int targetWidth = 900;
        var targetHeight = Math.Max(1, (int)Math.Round(targetWidth * (srcHeight / (double)srcWidth)));
        using var bitmap = new Bitmap(targetWidth, targetHeight);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, 0, 0, targetWidth, targetHeight);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return stream.ToArray();
    }

    private static void ApplyA4(PrintDocument document)
    {
        PaperSize? a4 = null;
        foreach (PaperSize size in document.PrinterSettings.PaperSizes)
        {
            if (size.Kind == PaperKind.A4)
            {
                a4 = size;
                break;
            }
        }

        document.DefaultPageSettings.PaperSize = a4 ?? new PaperSize("A4", 827, 1169);
    }

    private static System.Windows.Forms.IWin32Window? TryOwner()
    {
        var window = System.Windows.Application.Current?.MainWindow;
        if (window is null)
            return null;

        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        return handle == IntPtr.Zero ? null : new OwnerWindow(handle);
    }

    private sealed class OwnerWindow(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    private enum Phase
    {
        Header,
        Rows,
        Footer,
        Done
    }

    private sealed class PageCursor(NfceRelatorio report, DrawKit kit)
    {
        private const float RowHeight = 22f;
        private const float TitleHeight = 24f;
        private const float HeaderHeight = 22f;
        private Phase _phase = Phase.Header;
        private int _row;
        private int _pages;
        private bool _tableOpen;

        public void Draw(PrintPageEventArgs e)
        {
            _pages++;
            var graphics = e.Graphics ?? throw new InvalidOperationException("Impressora sem superfície de desenho.");
            graphics.PageUnit = GraphicsUnit.Point;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var top = e.MarginBounds.Top * 72f / 100f;
            var left = e.MarginBounds.Left * 72f / 100f;
            var width = e.MarginBounds.Width * 72f / 100f;
            var bottom = (e.MarginBounds.Top + e.MarginBounds.Height) * 72f / 100f;
            var y = top;

            if (_pages > 40)
            {
                e.HasMorePages = false;
                return;
            }

            if (_phase == Phase.Header)
            {
                y = DrawHeader(graphics, left, y, width);
                _phase = Phase.Rows;
            }

            if (_phase == Phase.Rows && DrawRows(graphics, ref y, left, width, top, bottom))
                _phase = Phase.Footer;

            if (_phase == Phase.Footer && (y + 36f <= bottom || y <= top + 0.5f))
            {
                DrawFooter(graphics, left, y + 8f, width);
                _phase = Phase.Done;
            }

            e.HasMorePages = _phase != Phase.Done;
        }

        private float DrawHeader(Graphics graphics, float x, float y, float width)
        {
            graphics.FillRectangle(kit.Accent, x, y, width, 8f);
            y += 18f;

            graphics.DrawString("RELATÓRIO DE NFC-e", kit.Title, Brushes.Black, x, y);
            var titleHeight = graphics.MeasureString("RELATÓRIO DE NFC-e", kit.Title).Height;

            var rightWidth = width * 0.42f;
            var info = new RectangleF(x + width - rightWidth, y, rightWidth, 18f);
            var terminal = string.IsNullOrWhiteSpace(report.TerminalCode) ? "Terminal —" : $"Terminal {report.TerminalCode}";
            graphics.DrawString(terminal, kit.Body, Brushes.Black, info, kit.Right);
            info.Y += 18f;
            var operador = string.IsNullOrWhiteSpace(report.OperatorName) ? "—" : report.OperatorName;
            graphics.DrawString($"Operador {operador}", kit.Body, Brushes.Black, info, kit.Right);

            var store = StoreLine();
            var storeY = y + titleHeight - 2f;
            if (store.Length > 0)
                graphics.DrawString(store, kit.Subtitle, Brushes.Black, new RectangleF(x, storeY, width * 0.55f, 22f), kit.Left);

            y = Math.Max(storeY + 22f, info.Y + 28f);

            var strip = new RectangleF(x, y, width, 28f);
            graphics.DrawRectangle(kit.Line, strip.X, strip.Y, strip.Width, strip.Height);
            graphics.DrawLine(kit.Line, x + width / 2f, y, x + width / 2f, y + 28f);
            graphics.DrawString(report.Periodo, kit.Body, Brushes.Black, new RectangleF(x + 8f, y, width / 2f - 16f, 28f), kit.Left);
            graphics.DrawString(report.Escopo, kit.Body, Brushes.Black, new RectangleF(x + width / 2f + 8f, y, width / 2f - 16f, 28f), kit.Left);
            y += 38f;

            var labels = new[] { "Autorizadas", "Contingências", "Canceladas", "No filtro" };
            var values = new[]
            {
                CashMovementTextFormatter.Money(report.TotalAutorizadas),
                CashMovementTextFormatter.Money(report.TotalContingencias),
                CashMovementTextFormatter.Money(report.TotalCanceladas),
                report.Total.ToString()
            };
            var notes = new[]
            {
                $"{report.Autorizadas} nota(s)",
                $"{report.Contingencias} nota(s)",
                $"{report.Canceladas} nota(s)",
                report.Rejeitadas == 0 ? "notas do filtro" : $"{report.Rejeitadas} rejeitada(s)"
            };

            const float gap = 8f;
            var cardWidth = (width - gap * 3f) / 4f;
            const float cardHeight = 86f;
            for (var i = 0; i < 4; i++)
            {
                var cardX = x + i * (cardWidth + gap);
                var pen = i == 3 ? kit.AccentPen : kit.Line;
                graphics.DrawRectangle(pen, cardX, y, cardWidth, cardHeight);
                graphics.DrawString(labels[i], kit.Small, kit.Muted, new RectangleF(cardX + 8f, y + 6f, cardWidth - 16f, 28f), kit.LeftWrap);
                graphics.DrawString(values[i], kit.CardValue, Brushes.Black, new RectangleF(cardX + 8f, y + 34f, cardWidth - 16f, 22f), kit.Left);
                graphics.DrawString(notes[i], kit.Tiny, kit.Muted, new RectangleF(cardX + 8f, y + 58f, cardWidth - 16f, 22f), kit.LeftWrap);
            }

            return y + cardHeight + 12f;
        }

        private bool DrawRows(Graphics graphics, ref float y, float x, float width, float top, float bottom)
        {
            var rows = Math.Max(1, report.Linhas.Count);
            var chrome = _tableOpen ? 0f : TitleHeight + HeaderHeight;
            if (y + chrome + RowHeight > bottom && y > top + 0.5f)
                return false;

            var tableTop = y;
            if (!_tableOpen)
            {
                graphics.FillRectangle(kit.HeaderFill, x, y, width, TitleHeight);
                graphics.DrawString("NFC-e", kit.BodyBold, Brushes.Black, new RectangleF(x, y, width, TitleHeight), kit.Center);
                y += TitleHeight;
                DrawColumns(graphics, x, y, width, header: true, line: null);
                y += HeaderHeight;
                _tableOpen = true;
            }

            while (_row < rows)
            {
                if (y + RowHeight > bottom)
                {
                    graphics.DrawRectangle(kit.Line, x, tableTop, width, y - tableTop);
                    _tableOpen = false;
                    return false;
                }

                if (report.Linhas.Count == 0)
                {
                    graphics.DrawString("Nenhuma NFC-e neste filtro.", kit.Small, kit.Muted, new RectangleF(x + 6f, y, width - 12f, RowHeight), kit.Left);
                    graphics.DrawLine(kit.Line, x, y + RowHeight, x + width, y + RowHeight);
                }
                else
                {
                    DrawColumns(graphics, x, y, width, header: false, report.Linhas[_row]);
                }

                y += RowHeight;
                _row++;
            }

            graphics.DrawRectangle(kit.Line, x, tableTop, width, y - tableTop);
            y += 8f;
            return true;
        }

        private void DrawColumns(Graphics graphics, float x, float y, float width, bool header, NfceRelatorioLinha? line)
        {
            const float dataWidth = 108f;
            const float numeroWidth = 72f;
            const float statusWidth = 92f;
            const float totalWidth = 88f;
            var vendaWidth = width - dataWidth - numeroWidth - statusWidth - totalWidth;
            var font = header ? kit.SmallBold : kit.Body;
            var brush = header ? kit.Muted : Brushes.Black;
            var rowHeight = header ? HeaderHeight : RowHeight;
            if (header)
                graphics.FillRectangle(kit.HeaderFill, x, y, width, HeaderHeight);

            var data = header ? "Emissão" : line is null ? string.Empty : line.EmitidoEm.ToString("dd/MM/yyyy HH:mm");
            var numero = header ? "Número" : line?.Numero ?? string.Empty;
            var status = header ? "Status" : line?.Status ?? string.Empty;
            var venda = header ? "Venda" : line?.Venda ?? string.Empty;
            var total = header
                ? "Total"
                : line?.Total is decimal valor ? CashMovementTextFormatter.Money(valor) : "—";

            var cursor = x;
            DrawCell(graphics, cursor, y, dataWidth, rowHeight, data, font, brush, kit.Left);
            cursor += dataWidth;
            DrawCell(graphics, cursor, y, numeroWidth, rowHeight, numero, font, brush, kit.Left);
            cursor += numeroWidth;
            DrawCell(graphics, cursor, y, statusWidth, rowHeight, status, font, brush, kit.Left);
            cursor += statusWidth;
            DrawCell(graphics, cursor, y, vendaWidth, rowHeight, venda, font, brush, kit.Left);
            cursor += vendaWidth;
            DrawCell(graphics, cursor, y, totalWidth, rowHeight, total, font, brush, kit.Right);
            graphics.DrawLine(kit.Line, x, y + rowHeight, x + width, y + rowHeight);
        }

        private static void DrawCell(
            Graphics graphics,
            float x,
            float y,
            float width,
            float height,
            string text,
            Font font,
            Brush brush,
            StringFormat format)
        {
            graphics.DrawString(text, font, brush, new RectangleF(x + 4f, y, width - 8f, height), format);
        }

        private void DrawFooter(Graphics graphics, float x, float y, float width)
        {
            if (report.Truncado)
            {
                graphics.DrawString("O relatório lista no máximo 500 notas do filtro.", kit.Small, kit.Muted, new RectangleF(x, y, width, 16f), kit.Left);
                y += 16f;
            }

            var printed = CashMovementTextFormatter.FormatDateTime(report.ImpressoEm);
            graphics.DrawString($"Impresso em {printed}", kit.Small, kit.Muted, new RectangleF(x, y, width, 18f), kit.Right);
        }

        private string StoreLine()
        {
            var name = report.StoreName.Trim();
            var code = report.StoreCode.Trim();
            if (name.Length == 0)
                return code;
            if (code.Length == 0)
                return name;
            return $"{code} · {name}";
        }
    }

    private sealed class DrawKit : IDisposable
    {
        public Font Title { get; } = new("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Point);
        public Font Subtitle { get; } = new("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
        public Font Body { get; } = new("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
        public Font BodyBold { get; } = new("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point);
        public Font SmallBold { get; } = new("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
        public Font CardValue { get; } = new("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Point);
        public Font Small { get; } = new("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
        public Font Tiny { get; } = new("Segoe UI", 7f, FontStyle.Regular, GraphicsUnit.Point);
        public StringFormat Left { get; } = Create(StringAlignment.Near, StringAlignment.Center, wrap: false);
        public StringFormat LeftWrap { get; } = Create(StringAlignment.Near, StringAlignment.Near, wrap: true);
        public StringFormat Right { get; } = Create(StringAlignment.Far, StringAlignment.Center, wrap: false);
        public StringFormat Center { get; } = Create(StringAlignment.Center, StringAlignment.Center, wrap: false);
        public Brush Muted { get; } = new SolidBrush(Color.FromArgb(90, 98, 110));
        public Brush HeaderFill { get; } = new SolidBrush(Color.FromArgb(245, 243, 240));
        public Brush Accent { get; } = new SolidBrush(Color.FromArgb(212, 88, 59));
        public Pen Line { get; } = new(Color.FromArgb(180, 186, 194), 0.8f);
        public Pen AccentPen { get; } = new(Color.FromArgb(212, 88, 59), 1.6f);

        public void Dispose()
        {
            Title.Dispose();
            Subtitle.Dispose();
            Body.Dispose();
            BodyBold.Dispose();
            SmallBold.Dispose();
            CardValue.Dispose();
            Small.Dispose();
            Tiny.Dispose();
            Left.Dispose();
            LeftWrap.Dispose();
            Right.Dispose();
            Center.Dispose();
            Muted.Dispose();
            HeaderFill.Dispose();
            Accent.Dispose();
            Line.Dispose();
            AccentPen.Dispose();
        }

        private static StringFormat Create(StringAlignment alignment, StringAlignment line, bool wrap)
            => new()
            {
                Alignment = alignment,
                LineAlignment = line,
                Trimming = wrap ? StringTrimming.EllipsisWord : StringTrimming.EllipsisCharacter,
                FormatFlags = wrap ? 0 : StringFormatFlags.NoWrap
            };
    }
}
