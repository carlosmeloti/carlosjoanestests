using AcaiPos.Domain.Entities;

namespace AcaiPos.Application.Sync;

public static class MontadorLote
{
    public static CorpoLote DeVenda(
        Sale venda,
        IReadOnlyList<SaleItem> itens,
        IReadOnlyList<Payment> pagamentos,
        Guid lojaId,
        Guid terminalId)
    {
        return new CorpoLote
        {
            Vendas =
            [
                new VendaLote
                {
                    Id = venda.Id,
                    CashRegisterId = venda.CashRegisterId,
                    TerminalId = terminalId,
                    StoreId = lojaId,
                    OperatorId = venda.OperatorId,
                    SoldAt = venda.SoldAt,
                    Subtotal = venda.Subtotal,
                    DiscountAmount = venda.DiscountAmount,
                    SurchargeAmount = venda.SurchargeAmount,
                    Total = venda.Total,
                    Status = venda.Status,
                    Origin = venda.Origin,
                    CancellationReason = venda.CancellationReason,
                    CancelledByOperatorId = venda.CancelledByOperatorId,
                    CancelledAt = venda.CancelledAt,
                    TicketNumber = venda.TicketNumber,
                    CpfConsumidor = venda.CpfConsumidor,
                    CreatedAt = venda.CreatedAt,
                    UpdatedAt = venda.UpdatedAt,
                    Itens = itens.Select(item => new ItemLote
                    {
                        Id = item.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        ProductSku = item.ProductSku,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountAmount = item.DiscountAmount,
                        Subtotal = item.Subtotal
                    }).ToList(),
                    Pagamentos = pagamentos.Select(pagamento => new PagamentoLote
                    {
                        Id = pagamento.Id,
                        FormaPagamentoId = pagamento.FormaPagamentoId,
                        Descricao = pagamento.Descricao,
                        Amount = pagamento.Amount,
                        AmountReceived = pagamento.AmountReceived,
                        ChangeAmount = pagamento.ChangeAmount,
                        ExternalReference = pagamento.ExternalReference,
                        Status = pagamento.Status,
                        PaidAt = pagamento.PaidAt
                    }).ToList()
                }
            ]
        };
    }

    public static CorpoLote DeCaixa(CashRegister caixa, Guid lojaId, Guid terminalId)
    {
        return new CorpoLote
        {
            Caixas =
            [
                new CaixaLote
                {
                    Id = caixa.Id,
                    TerminalId = terminalId,
                    StoreId = lojaId,
                    OperatorId = caixa.OperatorId,
                    OpenedAt = caixa.OpenedAt,
                    ClosedAt = caixa.ClosedAt,
                    OpeningAmount = caixa.OpeningAmount,
                    CountedAmount = caixa.CountedAmount,
                    ExpectedAmount = caixa.ExpectedAmount,
                    DifferenceAmount = caixa.DifferenceAmount,
                    Status = caixa.Status,
                    ClosedByOperatorId = caixa.ClosedByOperatorId,
                    ClosingNotes = caixa.ClosingNotes,
                    CreatedAt = caixa.CreatedAt,
                    UpdatedAt = caixa.UpdatedAt
                }
            ]
        };
    }

    public static CorpoLote DeMovimentoCaixa(CashMovement movimento, Guid terminalId)
    {
        return new CorpoLote
        {
            MovimentacoesCaixa =
            [
                new MovimentoCaixaLote
                {
                    Id = movimento.Id,
                    CashRegisterId = movimento.CashRegisterId,
                    TerminalId = terminalId,
                    OperatorId = movimento.OperatorId,
                    Type = movimento.Type,
                    Amount = movimento.Amount,
                    Reason = movimento.Reason,
                    OccurredAt = movimento.OccurredAt,
                    CreatedAt = movimento.CreatedAt,
                    UpdatedAt = movimento.UpdatedAt,
                    DeletedAt = movimento.DeletedAt
                }
            ]
        };
    }

    public static CorpoLote DeMovimentoEstoque(StockMovement movimento)
    {
        return new CorpoLote
        {
            MovimentacoesEstoque =
            [
                new MovimentoEstoqueLote
                {
                    Id = movimento.Id,
                    ProductId = movimento.ProductId,
                    SaleId = movimento.SaleId,
                    OperatorId = movimento.OperatorId,
                    Type = movimento.Type,
                    Quantity = movimento.Quantity,
                    PreviousQuantity = movimento.PreviousQuantity,
                    ResultingQuantity = movimento.ResultingQuantity,
                    Reason = movimento.Reason,
                    OccurredAt = movimento.OccurredAt,
                    CreatedAt = movimento.CreatedAt,
                    UpdatedAt = movimento.UpdatedAt,
                    DeletedAt = movimento.DeletedAt
                }
            ]
        };
    }
}
