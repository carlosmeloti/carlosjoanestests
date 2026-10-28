using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.IO;
using AcaiPos.Application.Models;
using AcaiPos.Application.Tickets;

namespace AcaiPos.Desktop.Printing;

/// <summary>
/// Relatório de movimento de caixa em A4 retrato, com cartões e tabelas.
/// </summary>
public sealed class CashMovementA4Printer
{
    /// <summary>
    /// Desenha as páginas A4 em memória, sem enviar à impressora.
    /// </summary>
    public IReadOnlyList<byte[]> RenderPreviewPngs(CashMovementReport report)
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
    public bool ShowAndPrint(CashMovementReport report)
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
            DocumentName = "Movimento de caixa"
        };
        document.DefaultPageSettings.Landscape = false;
        document.DefaultPageSettings.Margins = new Margins(48, 48, 48, 48);
        ApplyA4(document);
        return document;
    }

    private static void Attach(PrintDocument document, CashMovementReport report)
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
        Movements,
        Payments,
        Footer,
        Done
    }

    private sealed class PageCursor(CashMovementReport report, DrawKit kit)
    {
        private const float RowHeight = 22f;
        private const float TitleHeight = 24f;
        private const float HeaderHeight = 22f;
        private Phase _phase = Phase.Header;
        private int _movementRow;
        private int _paymentRow;
        private int _pages;

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

            var progressed = true;
            while (progressed && _phase != Phase.Done)
            {
                progressed = false;
                switch (_phase)
                {
                    case Phase.Header:
                        y = DrawHeader(graphics, left, y, width);
                        _phase = Phase.Movements;
                        progressed = true;
                        break;
                    case Phase.Movements:
                        if (DrawMovements(graphics, ref y, left, width, top, bottom))
                        {
                            _phase = Phase.Payments;
                            progressed = true;
                        }
                        break;
                    case Phase.Payments:
                        if (DrawPayments(graphics, ref y, left, width, top, bottom))
                        {
                            _phase = Phase.Footer;
                            progressed = true;
                        }
                        break;
                    case Phase.Footer:
                        if (y + 24f <= bottom || y <= top + 0.5f)
                        {
                            DrawFooter(graphics, left, y + 8f, width);
                            _phase = Phase.Done;
                            progressed = true;
                        }
                        break;
                }

                if (!progressed)
                    break;
            }

            e.HasMorePages = _phase != Phase.Done;
        }

        private float DrawHeader(Graphics graphics, float x, float y, float width)
        {
            var detail = report.Detail;
            graphics.FillRectangle(kit.Accent, x, y, width, 8f);
            y += 18f;

            graphics.DrawString("MOVIMENTO DE CAIXA", kit.Title, Brushes.Black, x, y);
            var titleHeight = graphics.MeasureString("MOVIMENTO DE CAIXA", kit.Title).Height;

            var rightWidth = width * 0.42f;
            var rightX = x + width - rightWidth;
            var info = new RectangleF(rightX, y, rightWidth, 18f);
            graphics.DrawString($"Terminal {detail.TerminalCode}", kit.Body, Brushes.Black, info, kit.Right);
            info.Y += 18f;
            graphics.DrawString($"Operador {detail.OperatorName}", kit.Body, Brushes.Black, info, kit.Right);

            var status = CashMovementTextFormatter.StatusLabel(detail);
            var statusSize = graphics.MeasureString(status, kit.SmallBold);
            var badgeWidth = statusSize.Width + 16f;
            var badgeHeight = statusSize.Height + 6f;
            var badge = new RectangleF(x + width - badgeWidth, info.Y + 20f, badgeWidth, badgeHeight);
            graphics.DrawRectangle(kit.AccentPen, badge.X, badge.Y, badge.Width, badge.Height);
            graphics.DrawString(status, kit.SmallBold, kit.Accent, badge, kit.Center);

            var store = StoreLine(report);
            var storeY = y + titleHeight - 2f;
            if (store.Length > 0)
                graphics.DrawString(store, kit.Subtitle, Brushes.Black, new RectangleF(x, storeY, width * 0.55f, 22f), kit.Left);

            y = Math.Max(storeY + 22f, badge.Bottom + 8f);

            var strip = new RectangleF(x, y, width, 28f);
            graphics.DrawRectangle(kit.Line, strip.X, strip.Y, strip.Width, strip.Height);
            graphics.DrawLine(kit.Line, x + width / 2f, y, x + width / 2f, y + 28f);
            var opened = CashMovementTextFormatter.FormatDateTime(detail.OpenedAt);
            var closed = CashMovementTextFormatter.ClosingLabel(detail);
            graphics.DrawString($"Abertura: {opened}", kit.Body, Brushes.Black, new RectangleF(x + 8f, y, width / 2f - 16f, 28f), kit.Left);
            graphics.DrawString($"Fechamento: {closed}", kit.Body, Brushes.Black, new RectangleF(x + width / 2f + 8f, y, width / 2f - 16f, 28f), kit.Left);
            y += 38f;

            var labels = new[]
            {
                "Fundo de abertura",
                "Saldo em dinheiro (gaveta)",
                "Saldo geral",
                "Total vendido"
            };
            var values = new[]
            {
                CashMovementTextFormatter.Money(detail.OpeningAmount),
                CashMovementTextFormatter.Money(detail.CashBalance),
                CashMovementTextFormatter.Money(detail.GeneralBalance),
                CashMovementTextFormatter.Money(detail.TotalSold)
            };
            var notes = new[]
            {
                null,
                CashMovementTextFormatter.CashBalanceFormula,
                CashMovementTextFormatter.GeneralBalanceFormula,
                null
            };

            const float gap = 8f;
            var cardWidth = (width - gap * 3f) / 4f;
            const float cardHeight = 86f;
            for (var i = 0; i < 4; i++)
            {
                var cardX = x + i * (cardWidth + gap);
                var pen = i == 2 ? kit.AccentPen : kit.Line;
                graphics.DrawRectangle(pen, cardX, y, cardWidth, cardHeight);
                graphics.DrawString(labels[i], kit.Small, kit.Muted, new RectangleF(cardX + 8f, y + 6f, cardWidth - 16f, 28f), kit.LeftWrap);
                graphics.DrawString(values[i], kit.CardValue, Brushes.Black, new RectangleF(cardX + 8f, y + 34f, cardWidth - 16f, 22f), kit.Left);
                if (notes[i] is not null)
                    graphics.DrawString(notes[i], kit.Tiny, kit.Muted, new RectangleF(cardX + 8f, y + 56f, cardWidth - 16f, 26f), kit.LeftWrap);
            }

            return y + cardHeight + 12f;
        }

        private bool DrawMovements(Graphics graphics, ref float y, float x, float width, float top, float bottom)
        {
            const float gap = 12f;
            var colWidth = (width - gap) / 2f;
            var rows = Math.Max(1, Math.Max(report.Detail.WithdrawalsList.Count, report.Detail.SuppliesList.Count));
            var chrome = TitleHeight + HeaderHeight;

            if (y + chrome + RowHeight > bottom && y > top + 0.5f)
                return false;

            var tableTop = y;
            DrawTableTitle(graphics, x, y, colWidth, "SANGRIAS");
            DrawTableTitle(graphics, x + colWidth + gap, y, colWidth, "SUPRIMENTOS");
            y += TitleHeight;
            DrawMovementColumns(graphics, x, y, colWidth, header: true, movement: null);
            DrawMovementColumns(graphics, x + colWidth + gap, y, colWidth, header: true, movement: null);
            y += HeaderHeight;

            var drewRow = false;
            while (_movementRow < rows)
            {
                if (y + RowHeight > bottom)
                {
                    CloseTables(graphics, x, tableTop, colWidth, gap, y);
                    return false;
                }

                DrawSide(graphics, x, y, colWidth, report.Detail.WithdrawalsList, CashMovementTextFormatter.NoWithdrawalsText);
                DrawSide(graphics, x + colWidth + gap, y, colWidth, report.Detail.SuppliesList, CashMovementTextFormatter.NoSuppliesText);
                y += RowHeight;
                _movementRow++;
                drewRow = true;
            }

            if (y + RowHeight > bottom && drewRow)
            {
                CloseTables(graphics, x, tableTop, colWidth, gap, y);
                return false;
            }

            DrawSubtotal(graphics, x, y, colWidth, CashMovementTextFormatter.Money(report.Detail.Withdrawals));
            DrawSubtotal(graphics, x + colWidth + gap, y, colWidth, CashMovementTextFormatter.Money(report.Detail.Supplies));
            y += RowHeight;
            CloseTables(graphics, x, tableTop, colWidth, gap, y);
            y += 12f;
            return true;
        }

        private void DrawSide(
            Graphics graphics,
            float x,
            float y,
            float width,
            IReadOnlyList<CashSessionMovementDto> movements,
            string emptyText)
        {
            if (movements.Count == 0)
            {
                if (_movementRow == 0)
                    graphics.DrawString(emptyText, kit.Small, kit.Muted, new RectangleF(x + 6f, y, width - 12f, RowHeight), kit.Left);
                graphics.DrawLine(kit.Line, x, y + RowHeight, x + width, y + RowHeight);
                return;
            }

            if (_movementRow < movements.Count)
                DrawMovementColumns(graphics, x, y, width, header: false, movements[_movementRow]);
            else
                graphics.DrawLine(kit.Line, x, y + RowHeight, x + width, y + RowHeight);
        }

        private bool DrawPayments(Graphics graphics, ref float y, float x, float width, float top, float bottom)
        {
            var payments = report.Detail.PaymentTotals;
            var rows = Math.Max(1, payments.Count);
            var chrome = TitleHeight + HeaderHeight;

            if (y + chrome + RowHeight > bottom && y > top + 0.5f)
                return false;

            var tableTop = y;
            graphics.FillRectangle(kit.HeaderFill, x, y, width, TitleHeight);
            graphics.DrawString("VENDAS POR FORMA DE PAGAMENTO", kit.BodyBold, Brushes.Black, new RectangleF(x, y, width, TitleHeight), kit.Center);
            y += TitleHeight;
            DrawPaymentRow(graphics, x, y, width, "Forma", "Valor", header: true, bold: false);
            y += HeaderHeight;

            while (_paymentRow < rows)
            {
                if (y + RowHeight > bottom)
                {
                    graphics.DrawRectangle(kit.Line, x, tableTop, width, y - tableTop);
                    return false;
                }

                if (payments.Count == 0)
                {
                    graphics.DrawString("Nenhuma venda neste ciclo.", kit.Small, kit.Muted, new RectangleF(x + 6f, y, width - 12f, RowHeight), kit.Left);
                    graphics.DrawLine(kit.Line, x, y + RowHeight, x + width, y + RowHeight);
                }
                else
                {
                    var payment = payments[_paymentRow];
                    var label = string.IsNullOrWhiteSpace(payment.Label) ? "—" : payment.Label.Trim();
                    DrawPaymentRow(graphics, x, y, width, label, CashMovementTextFormatter.Money(payment.Amount), header: false, bold: false);
                }

                y += RowHeight;
                _paymentRow++;
            }

            if (y + RowHeight > bottom && _paymentRow > 0)
            {
                graphics.DrawRectangle(kit.Line, x, tableTop, width, y - tableTop);
                return false;
            }

            DrawPaymentRow(
                graphics,
                x,
                y,
                width,
                "TOTAL VENDIDO",
                CashMovementTextFormatter.Money(report.Detail.TotalSold),
                header: false,
                bold: true);
            y += RowHeight;
            graphics.DrawRectangle(kit.Line, x, tableTop, width, y - tableTop);
            y += 8f;
            return true;
        }

        private void DrawFooter(Graphics graphics, float x, float y, float width)
        {
            var printed = CashMovementTextFormatter.FormatDateTime(report.PrintedAt);
            graphics.DrawString($"Impresso em {printed}", kit.Small, kit.Muted, new RectangleF(x, y, width, 18f), kit.Right);
        }

        private void DrawTableTitle(Graphics graphics, float x, float y, float width, string title)
        {
            graphics.FillRectangle(kit.HeaderFill, x, y, width, TitleHeight);
            graphics.DrawString(title, kit.BodyBold, Brushes.Black, new RectangleF(x, y, width, TitleHeight), kit.Center);
        }

        private void DrawMovementColumns(
            Graphics graphics,
            float x,
            float y,
            float width,
            bool header,
            CashSessionMovementDto? movement)
        {
            const float horaWidth = 48f;
            const float valorWidth = 78f;
            var motivoWidth = width - horaWidth - valorWidth;
            var font = header ? kit.SmallBold : kit.Body;
            var brush = header ? kit.Muted : Brushes.Black;
            if (header)
                graphics.FillRectangle(kit.HeaderFill, x, y, width, HeaderHeight);

            var hora = header ? "Hora" : movement is null ? string.Empty : CashMovementTextFormatter.FormatTime(movement.OccurredAt);
            var motivo = header ? "Motivo" : (movement?.Reason ?? string.Empty).Replace('\n', ' ').Trim();
            var valor = header ? "Valor" : movement is null ? string.Empty : CashMovementTextFormatter.Money(movement.Amount);
            var rowHeight = header ? HeaderHeight : RowHeight;

            graphics.DrawString(hora, font, brush, new RectangleF(x + 4f, y, horaWidth - 6f, rowHeight), kit.Left);
            graphics.DrawString(motivo, font, brush, new RectangleF(x + horaWidth, y, motivoWidth - 4f, rowHeight), kit.Left);
            graphics.DrawString(valor, font, brush, new RectangleF(x + horaWidth + motivoWidth, y, valorWidth - 6f, rowHeight), kit.Right);
            graphics.DrawLine(kit.Line, x, y + rowHeight, x + width, y + rowHeight);
        }

        private void DrawSubtotal(Graphics graphics, float x, float y, float width, string amount)
        {
            graphics.DrawString("Subtotal", kit.BodyBold, Brushes.Black, new RectangleF(x + 4f, y, width * 0.5f, RowHeight), kit.Left);
            graphics.DrawString(amount, kit.BodyBold, Brushes.Black, new RectangleF(x + width * 0.45f, y, width * 0.55f - 6f, RowHeight), kit.Right);
        }

        private void CloseTables(Graphics graphics, float x, float tableTop, float colWidth, float gap, float y)
        {
            var height = y - tableTop;
            graphics.DrawRectangle(kit.Line, x, tableTop, colWidth, height);
            graphics.DrawRectangle(kit.Line, x + colWidth + gap, tableTop, colWidth, height);
        }

        private void DrawPaymentRow(Graphics graphics, float x, float y, float width, string label, string amount, bool header, bool bold)
        {
            const float valorWidth = 120f;
            var font = bold || header ? kit.BodyBold : kit.Body;
            var brush = header ? kit.Muted : Brushes.Black;
            if (header)
                graphics.FillRectangle(kit.HeaderFill, x, y, width, HeaderHeight);

            var rowHeight = header ? HeaderHeight : RowHeight;
            graphics.DrawString(label, font, brush, new RectangleF(x + 8f, y, width - valorWidth - 12f, rowHeight), kit.Left);
            graphics.DrawString(amount, font, brush, new RectangleF(x + width - valorWidth, y, valorWidth - 8f, rowHeight), kit.Right);
            graphics.DrawLine(kit.Line, x, y + rowHeight, x + width, y + rowHeight);
        }

        private static string StoreLine(CashMovementReport source)
        {
            var name = source.StoreName.Trim();
            var code = source.StoreCode.Trim();
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
        public Font Small { get; } = new("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
        public Font SmallBold { get; } = new("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
        public Font CardValue { get; } = new("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Point);
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
            Small.Dispose();
            SmallBold.Dispose();
            CardValue.Dispose();
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
