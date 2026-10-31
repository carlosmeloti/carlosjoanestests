using AcaiPos.Domain.Entities;

namespace AcaiPos.Api.Repositories;

public interface ILojaRepository
{
    Task<Store?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken);

    void Adicionar(Store loja);
}
