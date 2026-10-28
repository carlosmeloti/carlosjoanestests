namespace NfeCliente;

public sealed record DadosEmitente(
    string Uf,
    bool Homologacao,
    string CertificadoArquivo,
    string CertificadoSenha,
    string? Csc = null,
    int IdTokenCsc = 0);

public sealed record EmitentePedido(
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

public sealed record ItemPedidoNfce(
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

public sealed record PagamentoPedidoNfce(
    string CodigoTpag,
    decimal Valor,
    string Descricao);

public sealed record PedidoNfce(
    EmitentePedido Emitente,
    DadosEmitente Credenciais,
    int Serie,
    int Numero,
    DateTimeOffset EmitidoEm,
    string? CpfConsumidor,
    IReadOnlyList<ItemPedidoNfce> Itens,
    IReadOnlyList<PagamentoPedidoNfce> Pagamentos,
    decimal Troco,
    bool Contingencia,
    string? JustificativaContingencia);

public sealed record ResultadoStatus(string Status, string Motivo)
{
    public bool ServicoOperando => Status == "107";
}

public sealed record ResultadoEnvio(string Status, string Motivo, string? XmlAutorizado);

public sealed record ResultadoCancelamento(
    string Status,
    string Motivo,
    string? Protocolo,
    string? XmlEvento)
{
    public bool Cancelada => Status is "135" or "155";
}

public sealed record ResultadoNfce(
    string Status,
    string Motivo,
    string? Xml,
    string? Chave,
    string? QrCode,
    string? UrlChave,
    string? Protocolo,
    bool Contingencia)
{
    public bool Autorizada => Status == "100";
}

public sealed class SemComunicacaoSefazException : Exception
{
    public SemComunicacaoSefazException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
