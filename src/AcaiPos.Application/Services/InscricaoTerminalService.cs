using AcaiPos.Application.Abstractions;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;

namespace AcaiPos.Application.Services;

public sealed record InscricaoLocal(string EnderecoApi, Guid LojaId, Guid TerminalId, bool TemSegredo);

public sealed class InscricaoTerminalService
{
    private readonly IPosDbContext _db;
    private readonly IProtecaoCertificado _protecao;

    public InscricaoTerminalService(IPosDbContext db, IProtecaoCertificado protecao)
    {
        _db = db;
        _protecao = protecao;
    }

    public Task<InscricaoLocal?> ObterAsync()
    {
        var atual = _db.InscricoesTerminais.FirstOrDefault();
        if (atual is null)
            return Task.FromResult<InscricaoLocal?>(null);

        return Task.FromResult<InscricaoLocal?>(new InscricaoLocal(
            atual.EnderecoApi,
            atual.LojaId,
            atual.TerminalId,
            atual.TemSegredo));
    }

    public async Task SalvarAsync(
        string enderecoApi,
        string lojaIdTexto,
        string terminalIdTexto,
        string? segredoClaro,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(lojaIdTexto?.Trim(), out var lojaId)
            || !Guid.TryParse(terminalIdTexto?.Trim(), out var terminalId))
            throw new DomainException("Loja e terminal da inscrição são obrigatórios.");

        var protegido = string.IsNullOrWhiteSpace(segredoClaro)
            ? null
            : _protecao.Proteger(segredoClaro.Trim());

        var atual = _db.InscricoesTerminais.FirstOrDefault();
        if (atual is null)
            _db.Add(InscricaoTerminal.Criar(enderecoApi, lojaId, terminalId, protegido ?? string.Empty));
        else
            atual.Atualizar(enderecoApi, lojaId, terminalId, protegido, manterSegredoSeVazio: true);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
