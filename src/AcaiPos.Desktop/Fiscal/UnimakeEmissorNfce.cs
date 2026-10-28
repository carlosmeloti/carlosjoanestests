using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using NfeCliente;

namespace AcaiPos.Desktop.Fiscal;

public sealed class UnimakeEmissorNfce : IEmissorNfce
{
    private readonly ClienteNfe _cliente;

    public UnimakeEmissorNfce(ClienteNfe cliente)
    {
        _cliente = cliente;
    }

    public Task<ResultadoEmissaoNfce> ConsultarStatusAsync(CredenciaisEmissao credenciais, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            try
            {
                var status = _cliente.ConsultarStatus(ParaCredenciais(credenciais));
                return new ResultadoEmissaoNfce(false, false, false, status.Status, status.Motivo, null, null, null, null, null);
            }
            catch (SemComunicacaoSefazException ex)
            {
                return new ResultadoEmissaoNfce(false, true, false, string.Empty, ex.Message, null, null, null, null, null);
            }
        }, cancellationToken);

    public Task<ResultadoEmissaoNfce> EmitirAsync(PedidoEmissaoNfce pedido, CancellationToken cancellationToken = default)
        => Task.Run(() => Executar(() => _cliente.Emitir(ParaPedido(pedido)), contingencia: false), cancellationToken);

    public Task<ResultadoEmissaoNfce> EmitirContingenciaAsync(PedidoEmissaoNfce pedido, CancellationToken cancellationToken = default)
        => Task.Run(() => Executar(() => _cliente.EmitirContingencia(ParaPedido(pedido)), contingencia: true), cancellationToken);

    public Task<ResultadoEmissaoNfce> TransmitirAsync(CredenciaisEmissao credenciais, string xml, CancellationToken cancellationToken = default)
        => Task.Run(() => Executar(() => _cliente.Transmitir(ParaCredenciais(credenciais), xml), contingencia: true), cancellationToken);

    public void ValidarIbsCbs(DateTimeOffset emissao, string ibsCbsCst, string cClassTrib)
        => NfceDocumento.ValidarIbsCbs(emissao, ibsCbsCst, cClassTrib);

    public Task<ResultadoCancelamentoNfce> CancelarAsync(CancelamentoNfce pedido, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            try
            {
                var resultado = _cliente.Cancelar(
                    ParaCredenciais(pedido.Credenciais),
                    pedido.Cnpj,
                    pedido.Chave,
                    pedido.Protocolo,
                    pedido.Justificativa);
                var cancelada = resultado.Cancelada
                    || (resultado.Status == "573" && !string.IsNullOrWhiteSpace(resultado.Protocolo));
                return new ResultadoCancelamentoNfce(
                    cancelada,
                    false,
                    resultado.Status,
                    resultado.Motivo,
                    resultado.Protocolo,
                    resultado.XmlEvento);
            }
            catch (SemComunicacaoSefazException ex)
            {
                return new ResultadoCancelamentoNfce(false, true, string.Empty, ex.Message, null, null);
            }
        }, cancellationToken);

    private static ResultadoEmissaoNfce Executar(Func<ResultadoNfce> acao, bool contingencia)
    {
        try
        {
            var resultado = acao();
            return new ResultadoEmissaoNfce(
                resultado.Autorizada,
                false,
                resultado.Contingencia || contingencia && !resultado.Autorizada,
                resultado.Status,
                resultado.Motivo,
                resultado.Xml,
                resultado.Chave,
                resultado.QrCode,
                resultado.UrlChave,
                resultado.Protocolo);
        }
        catch (SemComunicacaoSefazException ex)
        {
            return new ResultadoEmissaoNfce(false, true, false, string.Empty, ex.Message, null, null, null, null, null);
        }
    }

    private static DadosEmitente ParaCredenciais(CredenciaisEmissao credenciais) => new(
        credenciais.Uf,
        credenciais.Homologacao,
        credenciais.CertificadoArquivo,
        credenciais.CertificadoSenha,
        credenciais.Csc,
        credenciais.IdTokenCsc);

    private static PedidoNfce ParaPedido(PedidoEmissaoNfce pedido) => new(
        new EmitentePedido(
            pedido.Emitente.RazaoSocial,
            pedido.Emitente.NomeFantasia,
            pedido.Emitente.Cnpj,
            pedido.Emitente.InscricaoEstadual,
            pedido.Emitente.Crt,
            pedido.Emitente.Logradouro,
            pedido.Emitente.Numero,
            pedido.Emitente.Bairro,
            pedido.Emitente.CodigoMunicipio,
            pedido.Emitente.Municipio,
            pedido.Emitente.Cep,
            pedido.Emitente.Telefone),
        ParaCredenciais(pedido.Credenciais),
        pedido.Serie,
        pedido.Numero,
        pedido.EmitidoEm,
        pedido.CpfConsumidor,
        pedido.Itens.Select(item => new ItemPedidoNfce(
            item.Codigo,
            item.Descricao,
            item.CodigoBarras,
            item.Ncm,
            item.Cest,
            item.Cfop,
            item.Origem,
            item.Csosn,
            item.PisCst,
            item.CofinsCst,
            item.IbsCbsCst,
            item.CClassTrib,
            item.Unidade,
            item.Quantidade,
            item.ValorUnitario,
            item.Desconto,
            item.Acrescimo)).ToList(),
        pedido.Pagamentos.Select(pagamento => new PagamentoPedidoNfce(pagamento.CodigoTpag, pagamento.Valor, pagamento.Descricao)).ToList(),
        pedido.Troco,
        false,
        pedido.JustificativaContingencia);
}
