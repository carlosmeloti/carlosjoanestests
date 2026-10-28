using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using Unimake.Business.DFe.Security;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Xml.NFe;
using AutorizacaoNFe = Unimake.Business.DFe.Servicos.NFe.Autorizacao;
using AutorizacaoNfce = Unimake.Business.DFe.Servicos.NFCe.Autorizacao;
using RecepcaoEventoNfce = Unimake.Business.DFe.Servicos.NFCe.RecepcaoEvento;
using StatusNfce = Unimake.Business.DFe.Servicos.NFCe.StatusServico;

namespace NfeCliente;

public sealed class ClienteNfe
{
    public ResultadoStatus ConsultarStatus(DadosEmitente emitente)
    {
        var uf = UfParaEnum(emitente.Uf);
        var configuracao = CriarConfiguracao(emitente, TipoDFe.NFCe, TipoEmissao.Normal);
        var consulta = new ConsStatServ
        {
            Versao = "4.00",
            TpAmb = configuracao.TipoAmbiente,
            CUF = uf,
            XServ = "STATUS"
        };

        try
        {
            using var servico = new StatusNfce(consulta, configuracao);
            servico.Executar();
            return new ResultadoStatus(Texto(servico.Result?.CStat), servico.Result?.XMotivo ?? string.Empty);
        }
        catch (Exception ex) when (EhFalhaComunicacao(ex))
        {
            throw new SemComunicacaoSefazException("Sem comunicação com a SEFAZ.", ex);
        }
    }

    public ResultadoEnvio EnviarSincrono(DadosEmitente emitente, string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML da nota ou do lote é obrigatório.");

        var configuracao = CriarConfiguracao(emitente, TipoDFe.NFe, TipoEmissao.Normal);
        using var autorizacao = EhLote(xml)
            ? new AutorizacaoNFe(xml, configuracao)
            : new AutorizacaoNFe(MontarLote(xml), configuracao);

        autorizacao.Executar();

        string? xmlAutorizado = null;
        if (autorizacao.NfeProcResult != null)
            xmlAutorizado = autorizacao.NfeProcResult.GerarXML()?.OuterXml;

        return new ResultadoEnvio(
            Texto(autorizacao.Result?.CStat),
            autorizacao.Result?.XMotivo ?? string.Empty,
            xmlAutorizado);
    }

    public ResultadoNfce Emitir(PedidoNfce pedido)
    {
        var nota = NfceDocumento.Montar(pedido);
        var configuracao = CriarConfiguracao(pedido.Credenciais, TipoDFe.NFCe, TipoEmissao.Normal);
        var lote = new EnviNFe
        {
            Versao = "4.00",
            IdLote = DateTime.Now.ToString("yyyyMMddHHmmss"),
            IndSinc = SimNao.Sim,
            NFe = [nota]
        };

        try
        {
            using var autorizacao = new AutorizacaoNfce(lote, configuracao);
            autorizacao.Executar();
            return LerResultado(autorizacao, contingencia: false);
        }
        catch (SemComunicacaoSefazException)
        {
            throw;
        }
        catch (Exception ex) when (EhFalhaComunicacao(ex))
        {
            throw new SemComunicacaoSefazException("Sem comunicação com a SEFAZ.", ex);
        }
    }

    public ResultadoNfce EmitirContingencia(PedidoNfce pedido)
    {
        var pedidoOffline = pedido with
        {
            Contingencia = true,
            JustificativaContingencia = string.IsNullOrWhiteSpace(pedido.JustificativaContingencia)
                ? "SEFAZ indisponivel no momento da venda. Emissao em contingencia offline."
                : pedido.JustificativaContingencia
        };

        var documento = NfceDocumento.GerarXml(pedidoOffline);
        var configuracao = CriarConfiguracao(pedidoOffline.Credenciais, TipoDFe.NFCe, TipoEmissao.ContingenciaOffLine);
        PrepararUrls(documento.OuterXml, configuracao);
        Assinar(documento, configuracao);
        GerarQrCode(documento, configuracao);

        var xml = documento.OuterXml;
        var (qr, url, chave) = Extrair(xml);
        if (string.IsNullOrWhiteSpace(qr))
            throw new InvalidOperationException("Não foi possível gerar o QR Code da contingência.");

        return new ResultadoNfce("CONTINGENCIA", "Emitida em contingência offline.", xml, chave, qr, url, null, true);
    }

    public ResultadoCancelamento Cancelar(DadosEmitente emitente, string cnpj, string chave, string protocolo, string justificativa)
    {
        if (string.IsNullOrWhiteSpace(cnpj) || cnpj.Length != 14)
            throw new ArgumentException("CNPJ do emitente é obrigatório para cancelar a NFC-e.");
        if (string.IsNullOrWhiteSpace(chave) || chave.Length != 44)
            throw new ArgumentException("Chave da NFC-e é obrigatória para o cancelamento.");
        if (string.IsNullOrWhiteSpace(protocolo))
            throw new ArgumentException("Protocolo de autorização é obrigatório para o cancelamento.");

        var texto = justificativa.Trim();
        if (texto.Length is < 15 or > 255)
            throw new ArgumentException("A justificativa do cancelamento precisa ter entre 15 e 255 caracteres.");

        var uf = UfParaEnum(emitente.Uf);
        var configuracao = CriarConfiguracao(emitente, TipoDFe.NFCe, TipoEmissao.Normal);
        var infEvento = new InfEvento
        {
            Id = $"ID110111{chave}01",
            COrgao = uf,
            TpAmb = configuracao.TipoAmbiente,
            CNPJ = cnpj,
            ChNFe = chave,
            DhEvento = DateTimeOffset.Now,
            TpEvento = TipoEventoNFe.Cancelamento,
            NSeqEvento = 1,
            VerEvento = "1.00"
        };

        // O setter de DetEvento troca o objeto por um novo, conforme o tpEvento.
        infEvento.DetEvento = new DetEventoCanc();
        var detalhe = (DetEventoCanc)infEvento.DetEvento;
        detalhe.Versao = "1.00";
        detalhe.NProt = protocolo.Trim();
        detalhe.XJust = texto;

        var envEvento = new EnvEvento
        {
            Versao = "1.00",
            IdLote = DateTime.Now.ToString("yyyyMMddHHmmss"),
            Evento = [new Evento { Versao = "1.00", InfEvento = infEvento }]
        };

        try
        {
            using var recepcao = new RecepcaoEventoNfce(envEvento, configuracao);
            recepcao.Executar();
            var retorno = recepcao.Result?.RetEvento?.FirstOrDefault()?.InfEvento;
            var status = retorno is null ? Texto(recepcao.Result?.CStat) : retorno.CStat.ToString();
            var motivo = retorno?.XMotivo ?? recepcao.Result?.XMotivo ?? string.Empty;
            var xmlEvento = recepcao.ProcEventoNFeResult?.FirstOrDefault()?.GerarXML()?.OuterXml;
            return new ResultadoCancelamento(status, motivo, retorno?.NProt, xmlEvento);
        }
        catch (SemComunicacaoSefazException)
        {
            throw;
        }
        catch (Exception ex) when (EhFalhaComunicacao(ex))
        {
            throw new SemComunicacaoSefazException("Sem comunicação com a SEFAZ.", ex);
        }
    }

    public ResultadoNfce Transmitir(DadosEmitente emitente, string xmlAssinado)
    {
        if (string.IsNullOrWhiteSpace(xmlAssinado))
            throw new ArgumentException("XML assinado da NFC-e é obrigatório.");

        var configuracao = CriarConfiguracao(emitente, TipoDFe.NFCe, TipoEmissao.Normal);
        try
        {
            using var autorizacao = new AutorizacaoNfce(GarantirLote(xmlAssinado), configuracao);
            autorizacao.Executar();
            return LerResultado(autorizacao, contingencia: true);
        }
        catch (SemComunicacaoSefazException)
        {
            throw;
        }
        catch (Exception ex) when (EhFalhaComunicacao(ex))
        {
            throw new SemComunicacaoSefazException("Sem comunicação com a SEFAZ.", ex);
        }
    }

    public static UFBrasil UfParaEnum(string sigla)
    {
        if (string.IsNullOrWhiteSpace(sigla) || !Enum.TryParse(sigla.Trim(), true, out UFBrasil uf) || !Enum.IsDefined(uf))
            throw new ArgumentException("UF desconhecida: " + sigla);

        return uf;
    }

    private static ResultadoNfce LerResultado(AutorizacaoNfce autorizacao, bool contingencia)
    {
        var protocolo = autorizacao.NfeProcResult?.ProtNFe?.InfProt;
        var status = protocolo is null ? Texto(autorizacao.Result?.CStat) : protocolo.CStat.ToString();
        var motivo = protocolo?.XMotivo ?? autorizacao.Result?.XMotivo ?? string.Empty;
        string? xml = null;
        if (autorizacao.NfeProcResult != null)
            xml = autorizacao.NfeProcResult.GerarXML()?.OuterXml;
        xml ??= autorizacao.ConteudoXMLAssinado?.OuterXml;

        var (qr, url, chave) = Extrair(xml);
        chave ??= protocolo?.ChNFe;
        if (string.IsNullOrWhiteSpace(qr))
            (qr, url, _) = Extrair(autorizacao.ConteudoXMLAssinado?.OuterXml);

        return new ResultadoNfce(status, motivo, xml, chave, qr, url, protocolo?.NProt, contingencia);
    }

    private static void Assinar(XmlDocument documento, Configuracao configuracao)
    {
        using var certificado = X509CertificateLoader.LoadPkcs12FromFile(
            configuracao.CertificadoArquivo,
            configuracao.CertificadoSenha,
            X509KeyStorageFlags.EphemeralKeySet);
        AssinaturaDigital.Assinar(documento, "NFe", "infNFe", certificado, AlgorithmType.Sha1, true, "Id", false, false);
    }

    private static void GerarQrCode(XmlDocument documento, Configuracao configuracao)
    {
        var tipo = typeof(AutorizacaoNfce).Assembly
            .GetType("Unimake.Business.DFe.Xml.Validar.QRCode.Processors.QrCodeNFCeProcessor", throwOnError: true)!;
        var processador = Activator.CreateInstance(tipo)
            ?? throw new InvalidOperationException("Não foi possível criar o gerador de QR Code.");
        tipo.GetMethod("GerarQrCode")!.Invoke(processador, [documento, configuracao]);
    }

    private static void PrepararUrls(string xml, Configuracao configuracao)
    {
        var lote = EhLote(xml) ? xml : GarantirLote(xml);
        using var autorizacao = new AutorizacaoNfce(lote, configuracao);
        if (!string.IsNullOrWhiteSpace(configuracao.UrlQrCodeHomologacao) || !string.IsNullOrWhiteSpace(configuracao.UrlQrCodeProducao))
            return;

        var metodo = typeof(Unimake.Business.DFe.Servicos.ServicoBase)
            .GetMethod("DefinirConfiguracao", BindingFlags.Instance | BindingFlags.NonPublic);
        metodo?.Invoke(autorizacao, null);
    }

    private static Configuracao CriarConfiguracao(DadosEmitente emitente, TipoDFe tipo, TipoEmissao emissao)
    {
        if (string.IsNullOrWhiteSpace(emitente.CertificadoArquivo))
            throw new ArgumentException("Informe o certificado A1.");
        if (string.IsNullOrWhiteSpace(emitente.CertificadoSenha))
            throw new ArgumentException("Informe a senha do certificado.");

        var uf = UfParaEnum(emitente.Uf);
        var configuracao = new Configuracao
        {
            TipoDFe = tipo,
            TipoEmissao = emissao,
            TipoAmbiente = emitente.Homologacao ? TipoAmbiente.Homologacao : TipoAmbiente.Producao,
            CodigoUF = (int)uf,
            CertificadoArquivo = emitente.CertificadoArquivo,
            CertificadoSenha = emitente.CertificadoSenha,
            VersaoQRCodeNFCe = 2,
            TimeOutWebServiceConnect = 20000
        };

        if (!string.IsNullOrWhiteSpace(emitente.Csc))
        {
            configuracao.CSC = emitente.Csc.Trim();
            configuracao.CSCIDToken = emitente.IdTokenCsc;
        }

        return configuracao;
    }

    private static string GarantirLote(string xml)
    {
        if (EhLote(xml))
            return xml;

        var nota = xml.Trim().TrimStart('\uFEFF');
        if (nota.StartsWith("<?", StringComparison.Ordinal))
        {
            var fim = nota.IndexOf("?>", StringComparison.Ordinal);
            if (fim >= 0)
                nota = nota[(fim + 2)..].Trim();
        }

        var idLote = DateTime.Now.ToString("yyyyMMddHHmmss");
        return $"""
            <enviNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
              <idLote>{idLote}</idLote>
              <indSinc>1</indSinc>
              {nota}
            </enviNFe>
            """;
    }

    private static EnviNFe MontarLote(string xmlNota)
    {
        var nota = new NFe().LoadFromXML(xmlNota);
        var lote = new EnviNFe
        {
            Versao = "4.00",
            IdLote = DateTime.Now.ToString("yyyyMMddHHmmss"),
            IndSinc = SimNao.Sim
        };
        lote.NFe = [nota];
        return lote;
    }

    private static (string? QrCode, string? UrlChave, string? Chave) Extrair(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return (null, null, null);

        var documento = XDocument.Parse(xml);
        var qr = documento.Descendants().FirstOrDefault(elemento => elemento.Name.LocalName == "qrCode")?.Value?.Trim();
        var url = documento.Descendants().FirstOrDefault(elemento => elemento.Name.LocalName == "urlChave")?.Value?.Trim();
        var id = documento.Descendants().FirstOrDefault(elemento => elemento.Name.LocalName == "infNFe")
            ?.Attribute("Id")?.Value;
        var chave = id is not null && id.StartsWith("NFe", StringComparison.Ordinal) ? id[3..] : id;
        return (string.IsNullOrWhiteSpace(qr) ? null : qr, string.IsNullOrWhiteSpace(url) ? null : url, chave);
    }

    private static bool EhLote(string xml)
        => NomeRaiz(xml).Equals("enviNFe", StringComparison.OrdinalIgnoreCase);

    internal static string NomeRaiz(string xml)
    {
        var texto = xml.Trim().TrimStart('\uFEFF');
        var indice = 0;
        while (indice < texto.Length)
        {
            if (texto.AsSpan(indice).StartsWith("<?"))
            {
                var fim = texto.IndexOf("?>", indice, StringComparison.Ordinal);
                if (fim < 0)
                    break;

                indice = fim + 2;
                continue;
            }

            while (indice < texto.Length && char.IsWhiteSpace(texto[indice]))
                indice++;

            if (indice >= texto.Length)
                break;

            if (texto.AsSpan(indice).StartsWith("<!--"))
            {
                var fim = texto.IndexOf("-->", indice, StringComparison.Ordinal);
                if (fim < 0)
                    break;

                indice = fim + 3;
                continue;
            }

            if (texto[indice] == '<')
            {
                indice++;
                var inicio = indice;
                while (indice < texto.Length && !char.IsWhiteSpace(texto[indice]) && texto[indice] != '>' && texto[indice] != '/')
                    indice++;

                var tag = texto[inicio..indice];
                var separador = tag.IndexOf(':');
                return separador >= 0 ? tag[(separador + 1)..] : tag;
            }

            break;
        }

        throw new ArgumentException("XML sem elemento raiz.");
    }

    private static bool EhFalhaComunicacao(Exception ex)
    {
        for (Exception? atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is TimeoutException or HttpRequestException or System.Net.WebException or System.Net.Sockets.SocketException or IOException or TaskCanceledException)
                return true;

            var nome = atual.GetType().Name;
            if (nome.Contains("Comunicacao", StringComparison.OrdinalIgnoreCase)
                || nome.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
                || nome.Contains("Connection", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Texto(object? valor)
        => valor?.ToString() ?? string.Empty;
}
