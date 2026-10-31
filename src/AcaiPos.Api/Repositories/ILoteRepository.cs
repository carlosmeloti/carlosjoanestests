using AcaiPos.Domain.Entities;

namespace AcaiPos.Api.Repositories;

public interface ILoteRepository
{
    Task<Sale?> ObterVendaAsync(Guid id, CancellationToken cancellationToken);

    void Adicionar(Sale venda);

    Task<CashRegister?> ObterCaixaAsync(Guid id, CancellationToken cancellationToken);

    void Adicionar(CashRegister caixa);

    Task<CashMovement?> ObterMovimentoCaixaAsync(Guid id, CancellationToken cancellationToken);

    void Adicionar(CashMovement movimento);

    Task<StockMovement?> ObterEstoqueAsync(Guid id, CancellationToken cancellationToken);

    void Adicionar(StockMovement movimento);
}
