using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class DocumentoFiscal : AuditableEntity
{
    public Guid? SaleId { get; private set; }
    public int Modelo { get; private set; } = 65;
    public int Serie { get; private set; }
    public int Numero { get; private set; }
    public string? Chave { get; private set; }
    public int TipoEmissao { get; private set; } = 1;
    public StatusDocumentoFiscal Status { get; private set; }
    public string? Protocolo { get; private set; }
    public string? CStat { get; private set; }
    public string? Motivo { get; private set; }
    public string? QrCode { get; private set; }
    public string? UrlChave { get; private set; }
    public string? Xml { get; private set; }
    public string? ProtocoloCancelamento { get; private set; }
    public string? XmlEvento { get; private set; }
    public DateTime EmitidoEm { get; private set; }
    public DateTime? AutorizadoEm { get; private set; }
    public DateTime? CanceladoEm { get; private set; }

    private DocumentoFiscal()
    {
    }

    public static DocumentoFiscal Criar(
        int serie,
        int numero,
        int tipoEmissao,
        StatusDocumentoFiscal status,
        DateTime emitidoEm,
        string? chave,
        string? protocolo,
        string? cStat,
        string? motivo,
        string? qrCode,
        string? urlChave,
        string? xml)
    {
        if (serie <= 0 || numero <= 0)
            throw new DomainException("Série e número da NFC-e são obrigatórios.");

        return new DocumentoFiscal
        {
            Serie = serie,
            Numero = numero,
            TipoEmissao = tipoEmissao,
            Status = status,
            EmitidoEm = emitidoEm,
            Chave = chave,
            Protocolo = protocolo,
            CStat = cStat,
            Motivo = motivo,
            QrCode = qrCode,
            UrlChave = urlChave,
            Xml = xml,
            AutorizadoEm = status == StatusDocumentoFiscal.Autorizada ? DateTime.UtcNow : null
        };
    }

    public void VincularVenda(Guid saleId)
    {
        if (saleId == Guid.Empty)
            throw new DomainException("Venda inválida para o documento fiscal.");

        SaleId = saleId;
        MarkUpdated();
    }

    public void DesvincularVenda()
    {
        if (SaleId is null)
            return;

        SaleId = null;
        MarkUpdated();
    }

    public void ConfirmarTransmissao(string? protocolo, string? cStat, string? motivo, string? xml)
    {
        Status = StatusDocumentoFiscal.Autorizada;
        Protocolo = protocolo;
        CStat = cStat;
        Motivo = motivo;
        if (!string.IsNullOrWhiteSpace(xml))
            Xml = xml;
        AutorizadoEm = DateTime.UtcNow;
        MarkUpdated();
    }

    public void RegistrarFalhaTransmissao(string? cStat, string? motivo)
    {
        CStat = cStat;
        Motivo = motivo;
        MarkUpdated();
    }

    public void Cancelar(string? protocolo, string? cStat, string? motivo, string? xmlEvento)
    {
        Status = StatusDocumentoFiscal.Cancelada;
        ProtocoloCancelamento = protocolo;
        CStat = cStat;
        Motivo = motivo;
        XmlEvento = xmlEvento;
        CanceladoEm = DateTime.UtcNow;
        MarkUpdated();
    }

    public void CancelarSemTransmissao(string motivo)
    {
        Status = StatusDocumentoFiscal.Cancelada;
        Motivo = motivo;
        CanceladoEm = DateTime.UtcNow;
        MarkUpdated();
    }

    public void MarcarRejeitada(string? cStat, string? motivo)
    {
        Status = StatusDocumentoFiscal.Rejeitada;
        CStat = cStat;
        Motivo = motivo;
        MarkUpdated();
    }
}
