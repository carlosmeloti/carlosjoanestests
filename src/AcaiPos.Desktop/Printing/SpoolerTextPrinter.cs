using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using AcaiPos.Domain.Common;

namespace AcaiPos.Desktop.Printing;

/// <summary>
/// Envia texto puro à impressora padrão pelo spooler GDI, sem diálogo e sem ESC/POS.
/// </summary>
public sealed class SpoolerTextPrinter
{
    public const string MissingPrinterMessage = "Nenhuma impressora padrão configurada no Windows.";

    private const int Columns = 42;
    private const float MaxFontPt = 9f;
    private const float MinFontPt = 6f;

    /// <summary>
    /// Cabeça de impressão de uma bobina de 80 mm, em centésimos de polegada (72 mm).
    /// O driver costuma informar a largura do papel (80 mm), não a faixa que a cabeça alcança.
    /// </summary>
    private const float ThermalHead80MmHundredths = 284f;

    /// <summary>
    /// Folga à direita (~1 mm) para o glifo não encostar no fim da cabeça.
    /// </summary>
    private const float RightSafetyHundredths = 4f;

    private static readonly StringFormat LineFormat = CreateLineFormat();

    public void Print(string jobName, string text, Image? qrCode = null)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        using var document = new PrintDocument();
        if (!document.PrinterSettings.IsValid || string.IsNullOrWhiteSpace(document.PrinterSettings.PrinterName))
            throw new DomainException(MissingPrinterMessage);

        document.PrintController = new StandardPrintController();
        document.DocumentName = string.IsNullOrWhiteSpace(jobName) ? "AcaiPos" : jobName.Trim();
        document.PrinterSettings.Copies = 1;
        document.PrinterSettings.PrintToFile = false;

        var paperWidth = document.DefaultPageSettings.PaperSize.Width;
        if (paperWidth <= 40)
            paperWidth = 315;

        // Margem zero: a margem anterior empurrava as 42 colunas para a direita
        // e as duas últimas saíam da cabeça de impressão.
        document.DefaultPageSettings.Landscape = false;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.OriginAtMargins = false;
        document.DefaultPageSettings.PaperSize = new PaperSize("Bobina", paperWidth, 400);

        var fitHundredths = FitWidthHundredths(document.DefaultPageSettings, paperWidth);
        var fitWidthPt = fitHundredths * 72f / 100f;
        Font font;
        float lineHeightPt;
        using (var measure = document.PrinterSettings.CreateMeasurementGraphics())
        {
            measure.PageUnit = GraphicsUnit.Point;
            measure.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            font = FitFont(measure, fitWidthPt);
            lineHeightPt = font.GetHeight(measure);
        }

        var qrSizePt = qrCode is null ? 0f : Math.Min(fitWidthPt, 110f);
        var bodyHundredths = (int)Math.Ceiling((lines.Length * lineHeightPt + qrSizePt) * 100f / 72f);
        var pageHeight = Math.Max(bodyHundredths + 20, 40);
        document.DefaultPageSettings.PaperSize = new PaperSize("Bobina", paperWidth, pageHeight);

        document.PrintPage += (_, e) =>
        {
            var graphics = e.Graphics ?? throw new InvalidOperationException("Impressora sem superfície de desenho.");
            graphics.PageUnit = GraphicsUnit.Point;
            graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            var y = 1f;
            foreach (var line in lines)
            {
                graphics.DrawString(line, font, Brushes.Black, 0f, y, LineFormat);
                y += lineHeightPt;
            }

            if (qrCode is not null && qrSizePt > 0)
            {
                var x = Math.Max(0, (fitWidthPt - qrSizePt) / 2f);
                graphics.DrawImage(qrCode, x, y + 4f, qrSizePt, qrSizePt);
            }

            e.HasMorePages = false;
        };

        try
        {
            document.Print();
        }
        finally
        {
            font.Dispose();
        }
    }

    /// <summary>
    /// Largura em que as 42 colunas cabem de fato. Em bobina de 80 mm, limita à cabeça de 72 mm.
    /// </summary>
    internal static float FitWidthHundredths(PageSettings page, int paperWidth)
    {
        var width = (float)paperWidth;
        var area = page.PrintableArea;
        if (area.Width > 40f && area.Width <= paperWidth)
            width = area.Width;

        if (paperWidth is >= 300 and <= 340)
            width = Math.Min(width, ThermalHead80MmHundredths);

        return Math.Max(40f, width - RightSafetyHundredths);
    }

    private static Font FitFont(Graphics graphics, float printableWidthPt)
    {
        var sample = new string('0', Columns);

        for (var size = MaxFontPt; size >= MinFontPt; size -= 0.5f)
        {
            var font = new Font("Courier New", size, FontStyle.Regular, GraphicsUnit.Point);
            var measured = graphics.MeasureString(sample, font, int.MaxValue, LineFormat);
            if (measured.Width <= printableWidthPt)
                return font;

            font.Dispose();
        }

        return new Font("Courier New", MinFontPt, FontStyle.Regular, GraphicsUnit.Point);
    }

    private static StringFormat CreateLineFormat()
    {
        var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.None,
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near
        };
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces
            | StringFormatFlags.NoWrap
            | StringFormatFlags.NoClip;
        return format;
    }
}
