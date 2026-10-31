using AcaiPos.Api.Persistence;
using AcaiPos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Api.Repositories;

public sealed class LoteRepository : ILoteRepository
{
    private readonly CentralDbContext _db;

    public LoteRepository(CentralDbContext db)
    {
        _db = db;
    }

    public Task<Sale?> ObterVendaAsync(Guid id, CancellationToken cancellationToken)
        => _db.Sales
            .Include(item => item.Items)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public void Adicionar(Sale venda)
        => _db.Add(venda);

    public Task<CashRegister?> ObterCaixaAsync(Guid id, CancellationToken cancellationToken)
        => _db.CashRegisters.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public void Adicionar(CashRegister caixa)
        => _db.Add(caixa);

    public Task<CashMovement?> ObterMovimentoCaixaAsync(Guid id, CancellationToken cancellationToken)
        => _db.CashMovements.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public void Adicionar(CashMovement movimento)
        => _db.Add(movimento);

    public Task<StockMovement?> ObterEstoqueAsync(Guid id, CancellationToken cancellationToken)
        => _db.StockMovements.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public void Adicionar(StockMovement movimento)
        => _db.Add(movimento);
}
