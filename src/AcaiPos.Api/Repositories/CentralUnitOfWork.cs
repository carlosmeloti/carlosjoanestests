using AcaiPos.Api.Persistence;

namespace AcaiPos.Api.Repositories;

public sealed class CentralUnitOfWork : IUnitOfWork
{
    private readonly CentralDbContext _db;

    public CentralUnitOfWork(CentralDbContext db)
    {
        _db = db;
    }

    public Task SalvarAsync(CancellationToken cancellationToken)
        => _db.SaveChangesAsync(cancellationToken);

    public async Task ExecutarAsync(Func<Task> acao, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await acao();
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
