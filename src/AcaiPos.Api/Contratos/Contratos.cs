using AcaiPos.Domain.Enums;

namespace AcaiPos.Api.Contratos;

public sealed record InscricaoRequest(string CodigoLoja, string NomeLoja, string CodigoTerminal, string NomeTerminal);

public sealed record InscricaoResponse(Guid LojaId, Guid TerminalId, string Segredo);

public sealed record TokenRequest(Guid TerminalId, string Segredo);

public sealed record TokenResponse(string AccessToken, int ExpiresIn);

public sealed class LoteRequest
{
    public List<VendaLote> Vendas { get; set; } = [];
    public List<CaixaLote> Caixas { get; set; } = [];
    public List<MovimentoCaixaLote> MovimentacoesCaixa { get; set; } = [];
    public List<MovimentoEstoqueLote> MovimentacoesEstoque { get; set; } = [];
}

public sealed class VendaLote
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public Guid TerminalId { get; set; }
    public Guid StoreId { get; set; }
    public Guid OperatorId { get; set; }
    public DateTime? SoldAt { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal Total { get; set; }
    public SaleStatus Status { get; set; }
    public SaleOrigin Origin { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? CancelledByOperatorId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string? CpfConsumidor { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ItemLote> Itens { get; set; } = [];
    public List<PagamentoLote> Pagamentos { get; set; } = [];
}

public sealed class ItemLote
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
}

public sealed class PagamentoLote
{
    public Guid Id { get; set; }
    public int FormaPagamentoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? AmountReceived { get; set; }
    public decimal ChangeAmount { get; set; }
    public string? ExternalReference { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
}

public sealed class CaixaLote
{
    public Guid Id { get; set; }
    public Guid TerminalId { get; set; }
    public Guid StoreId { get; set; }
    public Guid OperatorId { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningAmount { get; set; }
    public decimal? CountedAmount { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public decimal? DifferenceAmount { get; set; }
    public CashRegisterStatus Status { get; set; }
    public Guid? ClosedByOperatorId { get; set; }
    public string? ClosingNotes { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class MovimentoCaixaLote
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public Guid TerminalId { get; set; }
    public Guid OperatorId { get; set; }
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? OccurredAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class MovimentoEstoqueLote
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? SaleId { get; set; }
    public Guid? OperatorId { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal PreviousQuantity { get; set; }
    public decimal ResultingQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? OccurredAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed record ItemAceito(string Tipo, Guid Id);

public abstract record ResultadoLote;

public sealed record LoteAceito(IReadOnlyList<ItemAceito> Aceitos) : ResultadoLote;

public sealed record LoteRecusado(string Mensagem) : ResultadoLote;
