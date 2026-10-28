using AcaiPos.Api.Contratos;
using AcaiPos.Api.Persistence;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Api.Lotes;

public sealed class LoteService
{
    private readonly CentralDbContext _db;
    private readonly ILogger<LoteService> _logger;

    public LoteService(CentralDbContext db, ILogger<LoteService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ResultadoLote> ReceberAsync(
        Guid terminalId,
        Guid storeId,
        LoteRequest lote,
        CancellationToken cancellationToken)
    {
        var recusa = Validar(lote, terminalId, storeId);
        if (recusa is not null)
        {
            _logger.LogWarning("Lote recusado do terminal {TerminalId}: {Mensagem}", terminalId, recusa);
            return new LoteRecusado(recusa);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var aceitos = new List<ItemAceito>();
            foreach (var venda in lote.Vendas)
            {
                await UpsertVendaAsync(venda, cancellationToken);
                aceitos.Add(new ItemAceito(nameof(SyncAggregateType.Sale), venda.Id));
            }

            foreach (var caixa in lote.Caixas)
            {
                await UpsertCaixaAsync(caixa, cancellationToken);
                aceitos.Add(new ItemAceito(nameof(SyncAggregateType.CashRegister), caixa.Id));
            }

            foreach (var movimento in lote.MovimentacoesCaixa)
            {
                await UpsertMovimentoCaixaAsync(movimento, cancellationToken);
                aceitos.Add(new ItemAceito(nameof(SyncAggregateType.CashMovement), movimento.Id));
            }

            foreach (var movimento in lote.MovimentacoesEstoque)
            {
                await UpsertEstoqueAsync(movimento, cancellationToken);
                aceitos.Add(new ItemAceito(nameof(SyncAggregateType.StockMovement), movimento.Id));
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            foreach (var item in aceitos)
                _logger.LogInformation(
                    "Lote do terminal {TerminalId} aceitou {Tipo} {AggregateId}",
                    terminalId,
                    item.Tipo,
                    item.Id);

            return new LoteAceito(aceitos);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning("Lote recusado do terminal {TerminalId}: {Mensagem}", terminalId, ex.Message);
            return new LoteRecusado(ex.Message);
        }
    }

    private async Task UpsertVendaAsync(VendaLote venda, CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var itens = venda.Itens.Select(item => SaleItem.Restaurar(
            item.Id,
            venda.Id,
            item.ProductId,
            item.ProductName,
            item.ProductSku,
            item.Quantity,
            item.UnitPrice,
            item.DiscountAmount,
            item.Subtotal)).ToList();
        var pagamentos = venda.Pagamentos.Select(pagamento => Payment.Restaurar(
            pagamento.Id,
            venda.Id,
            pagamento.FormaPagamentoId,
            pagamento.Descricao,
            pagamento.Amount,
            pagamento.AmountReceived,
            pagamento.ChangeAmount,
            pagamento.ExternalReference,
            pagamento.Status,
            pagamento.PaidAt ?? agora)).ToList();

        var existente = await _db.Sales
            .Include(item => item.Items)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.Id == venda.Id, cancellationToken);

        if (existente is null)
        {
            _db.Add(Sale.Restaurar(
                venda.Id,
                venda.CashRegisterId,
                venda.TerminalId,
                venda.StoreId,
                venda.OperatorId,
                venda.SoldAt ?? agora,
                venda.Subtotal,
                venda.DiscountAmount,
                venda.SurchargeAmount,
                venda.Total,
                venda.Status,
                venda.Origin,
                venda.CancellationReason,
                venda.CancelledByOperatorId,
                venda.CancelledAt,
                venda.TicketNumber,
                venda.CpfConsumidor,
                venda.CreatedAt ?? agora,
                venda.UpdatedAt ?? agora,
                itens,
                pagamentos));
            return;
        }

        existente.AplicarEspelho(
            venda.CashRegisterId,
            venda.TerminalId,
            venda.StoreId,
            venda.OperatorId,
            venda.SoldAt ?? agora,
            venda.Subtotal,
            venda.DiscountAmount,
            venda.SurchargeAmount,
            venda.Total,
            venda.Status,
            venda.Origin,
            venda.CancellationReason,
            venda.CancelledByOperatorId,
            venda.CancelledAt,
            venda.TicketNumber,
            venda.CpfConsumidor,
            venda.CreatedAt ?? existente.CreatedAt,
            venda.UpdatedAt ?? agora,
            itens,
            pagamentos);
    }

    private async Task UpsertCaixaAsync(CaixaLote caixa, CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var existente = await _db.CashRegisters.FirstOrDefaultAsync(item => item.Id == caixa.Id, cancellationToken);
        if (existente is null)
        {
            _db.Add(CashRegister.Restaurar(
                caixa.Id,
                caixa.TerminalId,
                caixa.StoreId,
                caixa.OperatorId,
                caixa.OpenedAt ?? agora,
                caixa.ClosedAt,
                caixa.OpeningAmount,
                caixa.CountedAmount,
                caixa.ExpectedAmount,
                caixa.DifferenceAmount,
                caixa.Status,
                caixa.ClosedByOperatorId,
                caixa.ClosingNotes,
                caixa.CreatedAt ?? agora,
                caixa.UpdatedAt ?? agora));
            return;
        }

        existente.AplicarEspelho(
            caixa.TerminalId,
            caixa.StoreId,
            caixa.OperatorId,
            caixa.OpenedAt ?? agora,
            caixa.ClosedAt,
            caixa.OpeningAmount,
            caixa.CountedAmount,
            caixa.ExpectedAmount,
            caixa.DifferenceAmount,
            caixa.Status,
            caixa.ClosedByOperatorId,
            caixa.ClosingNotes,
            caixa.CreatedAt ?? existente.CreatedAt,
            caixa.UpdatedAt ?? agora);
    }

    private async Task UpsertMovimentoCaixaAsync(MovimentoCaixaLote movimento, CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var existente = await _db.CashMovements.FirstOrDefaultAsync(item => item.Id == movimento.Id, cancellationToken);
        if (existente is null)
        {
            _db.Add(CashMovement.Restaurar(
                movimento.Id,
                movimento.CashRegisterId,
                movimento.TerminalId,
                movimento.OperatorId,
                movimento.Type,
                movimento.Amount,
                movimento.Reason,
                movimento.OccurredAt ?? agora,
                movimento.CreatedAt ?? agora,
                movimento.UpdatedAt ?? agora,
                movimento.DeletedAt));
            return;
        }

        existente.AplicarEspelho(
            movimento.CashRegisterId,
            movimento.TerminalId,
            movimento.OperatorId,
            movimento.Type,
            movimento.Amount,
            movimento.Reason,
            movimento.OccurredAt ?? agora,
            movimento.CreatedAt ?? existente.CreatedAt,
            movimento.UpdatedAt ?? agora,
            movimento.DeletedAt);
    }

    private async Task UpsertEstoqueAsync(MovimentoEstoqueLote movimento, CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var existente = await _db.StockMovements.FirstOrDefaultAsync(item => item.Id == movimento.Id, cancellationToken);
        if (existente is null)
        {
            _db.Add(StockMovement.Restaurar(
                movimento.Id,
                movimento.ProductId,
                movimento.SaleId,
                movimento.OperatorId,
                movimento.Type,
                movimento.Quantity,
                movimento.PreviousQuantity,
                movimento.ResultingQuantity,
                movimento.Reason,
                movimento.OccurredAt ?? agora,
                movimento.CreatedAt ?? agora,
                movimento.UpdatedAt ?? agora,
                movimento.DeletedAt));
            return;
        }

        existente.AplicarEspelho(
            movimento.ProductId,
            movimento.SaleId,
            movimento.OperatorId,
            movimento.Type,
            movimento.Quantity,
            movimento.PreviousQuantity,
            movimento.ResultingQuantity,
            movimento.Reason,
            movimento.OccurredAt ?? agora,
            movimento.CreatedAt ?? existente.CreatedAt,
            movimento.UpdatedAt ?? agora,
            movimento.DeletedAt);
    }

    private static string? Validar(LoteRequest lote, Guid terminalId, Guid storeId)
    {
        if (lote.Vendas.Count == 0
            && lote.Caixas.Count == 0
            && lote.MovimentacoesCaixa.Count == 0
            && lote.MovimentacoesEstoque.Count == 0)
            return "Lote vazio.";

        if (IdsRepetidos(lote))
            return "Lote repete o identificador de um agregado.";

        foreach (var venda in lote.Vendas)
        {
            if (venda.StoreId != storeId || venda.TerminalId != terminalId)
                return "Lote de outra loja.";
            if (venda.Status is not (SaleStatus.Completed or SaleStatus.Cancelled))
                return "Somente venda concluída ou cancelada entra no lote.";
            if (venda.Itens.Count == 0)
                return "Venda concluída exige itens.";
        }

        foreach (var caixa in lote.Caixas)
        {
            if (caixa.StoreId != storeId || caixa.TerminalId != terminalId)
                return "Lote de outra loja.";
        }

        foreach (var movimento in lote.MovimentacoesCaixa)
        {
            if (movimento.TerminalId != terminalId)
                return "Lote de outra loja.";
        }

        return null;
    }

    private static bool IdsRepetidos(LoteRequest lote)
    {
        return Repete(lote.Vendas.Select(item => item.Id))
            || Repete(lote.Caixas.Select(item => item.Id))
            || Repete(lote.MovimentacoesCaixa.Select(item => item.Id))
            || Repete(lote.MovimentacoesEstoque.Select(item => item.Id));
    }

    private static bool Repete(IEnumerable<Guid> ids)
    {
        var vistos = new HashSet<Guid>();
        return ids.Any(id => !vistos.Add(id));
    }
}
