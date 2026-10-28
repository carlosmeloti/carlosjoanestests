using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace AcaiPos.Desktop.Printing;

public static class QrCodeImagem
{
    public static byte[] Png(string conteudo)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(dados).GetGraphic(8);
    }

    public static BitmapImage ParaTela(string conteudo)
    {
        var imagem = new BitmapImage();
        using var stream = new MemoryStream(Png(conteudo));
        imagem.BeginInit();
        imagem.CacheOption = BitmapCacheOption.OnLoad;
        imagem.StreamSource = stream;
        imagem.EndInit();
        imagem.Freeze();
        return imagem;
    }

    public static Bitmap ParaImpressao(string conteudo)
    {
        using var stream = new MemoryStream(Png(conteudo));
        using var carregada = Image.FromStream(stream);
        return new Bitmap(carregada);
    }
}
