using AcaiPos.Api.Persistence;
using AcaiPos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Api.Repositories;

public sealed class TerminalRepository : ITerminalRepository
{
    private readonly CentralDbContext _db;

    public TerminalRepository(CentralDbContext db)
    {
        _db = db;
    }

    public Task<Terminal?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
        => _db.Terminals.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<Terminal?> ObterPorLojaECodigoAsync(Guid lojaId, string codigo, CancellationToken cancellationToken)
        => _db.Terminals.FirstOrDefaultAsync(
            item => item.StoreId == lojaId && item.Code == codigo,
            cancellationToken);

    public void Adicionar(Terminal terminal)
        => _db.Add(terminal);

    public string? LerSegredoHash(Terminal terminal)
        => _db.Entry(terminal).Property<string?>("SegredoHash").CurrentValue;

    public void GravarSegredo(Terminal terminal, string hash, DateTime emitidoEm)
    {
        var entrada = _db.Entry(terminal);
        entrada.Property<string?>("SegredoHash").CurrentValue = hash;
        entrada.Property<DateTime?>("SegredoEmitidoEm").CurrentValue = emitidoEm;
    }
}
