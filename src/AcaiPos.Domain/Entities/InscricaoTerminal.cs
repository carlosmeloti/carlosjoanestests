using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class InscricaoTerminal : Entity
{
    public string EnderecoApi { get; private set; } = string.Empty;
    public Guid LojaId { get; private set; }
    public Guid TerminalId { get; private set; }
    public string SegredoProtegido { get; private set; } = string.Empty;
    public DateTime? CatalogoLidoEm { get; private set; }

    private InscricaoTerminal()
    {
    }

    public bool TemSegredo => !string.IsNullOrWhiteSpace(SegredoProtegido);

    public bool Completa =>
        !string.IsNullOrWhiteSpace(EnderecoApi)
        && LojaId != Guid.Empty
        && TerminalId != Guid.Empty
        && TemSegredo;

    public static InscricaoTerminal Criar(string enderecoApi, Guid lojaId, Guid terminalId, string segredoProtegido)
    {
        var inscricao = new InscricaoTerminal();
        inscricao.Atualizar(enderecoApi, lojaId, terminalId, segredoProtegido, manterSegredoSeVazio: false);
        return inscricao;
    }

    public void Atualizar(string enderecoApi, Guid lojaId, Guid terminalId, string? segredoProtegido, bool manterSegredoSeVazio)
    {
        if (string.IsNullOrWhiteSpace(enderecoApi))
            throw new DomainException("Endereço da API é obrigatório.");

        if (!Uri.TryCreate(enderecoApi.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainException("Endereço da API é inválido.");

        if (lojaId == Guid.Empty || terminalId == Guid.Empty)
            throw new DomainException("Loja e terminal da inscrição são obrigatórios.");

        var informaSegredo = !string.IsNullOrWhiteSpace(segredoProtegido);
        if (!informaSegredo && !(manterSegredoSeVazio && TemSegredo))
            throw new DomainException("Segredo da inscrição é obrigatório.");

        EnderecoApi = enderecoApi.Trim().TrimEnd('/');
        LojaId = lojaId;
        TerminalId = terminalId;
        if (informaSegredo)
            SegredoProtegido = segredoProtegido!.Trim();
    }

    public void MarcarCatalogoLido(DateTime instante) => CatalogoLidoEm = instante;
}
