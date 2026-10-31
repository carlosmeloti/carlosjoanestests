using AcaiPos.Api.Persistence;
using AcaiPos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Api.Repositories;

public sealed class LojaRepository : ILojaRepository
{
    private readonly CentralDbContext _db;

    public LojaRepository(CentralDbContext db)
    {
        _db = db;
    }

    public Task<Store?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken)
        => _db.Stores.FirstOrDefaultAsync(item => item.Code == codigo, cancellationToken);

    public void Adicionar(Store loja)
        => _db.Add(loja);
}
