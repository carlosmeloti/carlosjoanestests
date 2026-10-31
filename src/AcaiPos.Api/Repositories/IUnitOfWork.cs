namespace AcaiPos.Api.Repositories;

public interface IUnitOfWork
{
    Task SalvarAsync(CancellationToken cancellationToken);

    Task ExecutarAsync(Func<Task> acao, CancellationToken cancellationToken);
}
