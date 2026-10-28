using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Models;

public sealed record CredenciaisEmissao(
    string Uf,
    bool Homologacao,
    string CertificadoArquivo,
    string CertificadoSenha,
    string Csc,
    int IdTokenCsc);

public sealed record EmitenteEmissao(
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string InscricaoEstadual,
    int Crt,
    string Logradouro,
    string Numero,
    string Bairro,
    int CodigoMunicipio,
    string Municipio,
    string Cep,
    string? Telefone);

public sealed record ItemEmissao(
    string Codigo,
    string Descricao,
    string? CodigoBarras,
    string Ncm,
    string? Cest,
    string Cfop,
    int Origem,
    string Csosn,
    string PisCst,
    string CofinsCst,
    string IbsCbsCst,
    string CClassTrib,
    string Unidade,
    decimal Quantidade,
    decimal ValorUnitario,
    decimal Desconto,
    decimal Acrescimo);

public sealed record PagamentoEmissao(
    string CodigoTpag,
    decimal Valor,
    string Descricao);

public sealed record PedidoEmissaoNfce(
    EmitenteEmissao Emitente,
    CredenciaisEmissao Credenciais,
    int Serie,
    int Numero,
    DateTimeOffset EmitidoEm,
    string? CpfConsumidor,
    IReadOnlyList<ItemEmissao> Itens,
    IReadOnlyList<PagamentoEmissao> Pagamentos,
    decimal Troco,
    string? JustificativaContingencia);

public sealed record ResultadoEmissaoNfce(
    bool Autorizada,
    bool SemComunicacao,
    bool Contingencia,
    string Status,
    string Motivo,
    string? Xml,
    string? Chave,
    string? QrCode,
    string? UrlChave,
    string? Protocolo)
{
    public bool ServicoOperando => Status == "107";
}

public sealed record ConfiguracaoFiscalDto(
    Guid Id,
    Guid StoreId,
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string InscricaoEstadual,
    int Crt,
    string Logradouro,
    string Numero,
    string Bairro,
    int CodigoMunicipio,
    string Municipio,
    string Uf,
    string Cep,
    string? Telefone,
    string Csc,
    int IdTokenCsc,
    bool Homologacao,
    int Serie,
    int ProximoNumero,
    string? CertificadoArquivo,
    bool TemSenhaCertificado,
    string JustificativaContingencia,
    bool EmitirPorPadrao);

public sealed record SalvarConfiguracaoFiscalRequest(
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string InscricaoEstadual,
    int Crt,
    string Logradouro,
    string Numero,
    string Bairro,
    int CodigoMunicipio,
    string Municipio,
    string Uf,
    string Cep,
    string? Telefone,
    string Csc,
    int IdTokenCsc,
    bool Homologacao,
    int Serie,
    int ProximoNumero,
    string? CertificadoArquivo,
    string? SenhaCertificado,
    string JustificativaContingencia,
    bool EmitirPorPadrao);

public sealed record ContingenciaPendenteDto(
    Guid Id,
    int Serie,
    int Numero,
    string? Chave,
    DateTime EmitidoEm,
    string? Motivo);

public sealed record TransmissaoContingenciaDto(
    int Autorizadas,
    int Pendentes,
    int Rejeitadas,
    string Mensagem);

public sealed record CancelamentoNfce(
    CredenciaisEmissao Credenciais,
    string Cnpj,
    string Chave,
    string Protocolo,
    string Justificativa);

public sealed record ResultadoCancelamentoNfce(
    bool Cancelada,
    bool SemComunicacao,
    string Status,
    string Motivo,
    string? Protocolo,
    string? XmlEvento);

public sealed record XmlNfceExportado(string NomeArquivo, string Conteudo);

public sealed record NfceGestaoConsulta(
    bool SomenteCaixaAberto,
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    StatusDocumentoFiscal? Status,
    string? Ticket,
    int Pagina,
    int Tamanho);

public sealed record NfceGestaoItemDto(
    Guid Id,
    Guid? SaleId,
    string Numero,
    DateTime EmitidoEm,
    string StatusLabel,
    StatusDocumentoFiscal Status,
    string TicketNumber,
    decimal? Total,
    string? Chave,
    string? Protocolo,
    string? CStat,
    string? Motivo,
    bool PodeEnviar,
    bool PodeExportarXml,
    bool PodeCancelar,
    bool CancelaNaSefaz,
    bool CancelaContingencia);

public sealed record NfceGestaoPaginaDto(
    IReadOnlyList<NfceGestaoItemDto> Itens,
    int Total,
    int Pagina,
    int Tamanho,
    int Paginas,
    string Mensagem);

public sealed record NfceRelatorioLinha(
    DateTime EmitidoEm,
    string Numero,
    string Status,
    string Venda,
    decimal? Total);

public sealed record NfceRelatorio(
    string StoreCode,
    string StoreName,
    string TerminalCode,
    string OperatorName,
    string Escopo,
    string Periodo,
    int Autorizadas,
    decimal TotalAutorizadas,
    int Contingencias,
    decimal TotalContingencias,
    int Canceladas,
    decimal TotalCanceladas,
    int Rejeitadas,
    int Total,
    bool Truncado,
    IReadOnlyList<NfceRelatorioLinha> Linhas,
    DateTime ImpressoEm);
