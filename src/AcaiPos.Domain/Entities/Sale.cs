using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class Sale : AuditableEntity
{
    public Guid CashRegisterId { get; private set; }
    public Guid TerminalId { get; private set; }
    public Guid OperatorId { get; private set; }
    public DateTime SoldAt { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal Total { get; private set; }
    public SaleStatus Status { get; private set; }
    public SaleOrigin Origin { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? CancelledByOperatorId { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string TicketNumber { get; private set; } = string.Empty;

    public CashRegister? CashRegister { get; private set; }
    public User? Operator { get; private set; }

    private readonly List<SaleItem> _items = [];
    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

    private readonly List<Payment> _payments = [];
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    private Sale()
    {
    }

    public static Sale Create(
        Guid cashRegisterId,
        Guid terminalId,
        Guid operatorId,
        string ticketNumber,
        SaleOrigin origin = SaleOrigin.Pos)
    {
        if (cashRegisterId == Guid.Empty)
            throw new DomainException("Caixa é obrigatório.");
        if (terminalId == Guid.Empty)
            throw new DomainException("Terminal é obrigatório.");
        if (operatorId == Guid.Empty)
            throw new DomainException("Operador é obrigatório.");
        if (string.IsNullOrWhiteSpace(ticketNumber))
            throw new DomainException("Número do ticket é obrigatório.");

        return new Sale
        {
            CashRegisterId = cashRegisterId,
            TerminalId = terminalId,
            OperatorId = operatorId,
            SoldAt = DateTime.UtcNow,
            Status = SaleStatus.Open,
            Origin = origin,
            TicketNumber = ticketNumber.Trim(),
            Subtotal = 0,
            DiscountAmount = 0,
            Total = 0
        };
    }

    public SaleItem AddItem(Product product, decimal quantity)
    {
        EnsureOpen();
        if (!product.IsActive || product.IsDeleted)
            throw new DomainException("Produto inativo não pode ser vendido.");
        if (quantity <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");

        var existing = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
            RecalculateTotals();
            return existing;
        }

        var item = SaleItem.Create(Id, product, quantity);
        _items.Add(item);
        RecalculateTotals();
        return item;
    }

    public void UpdateItemQuantity(Guid saleItemId, decimal quantity)
    {
        EnsureOpen();
        var item = GetItem(saleItemId);
        if (quantity <= 0)
        {
            _items.Remove(item);
        }
        else
        {
            item.SetQuantity(quantity);
        }

        RecalculateTotals();
    }

    public void RemoveItem(Guid saleItemId)
    {
        EnsureOpen();
        var item = GetItem(saleItemId);
        _items.Remove(item);
        RecalculateTotals();
    }

    public void ApplyDiscount(decimal discountAmount)
    {
        EnsureOpen();
        if (discountAmount < 0)
            throw new DomainException("Desconto não pode ser negativo.");
        if (discountAmount > Subtotal)
            throw new DomainException("Desconto não pode ser maior que o subtotal.");

        DiscountAmount = decimal.Round(discountAmount, 2);
        RecalculateTotals();
    }

    public Payment AddPayment(PaymentMethod method, decimal amount, decimal? amountReceived = null)
    {
        EnsureOpen();
        if (amount <= 0)
            throw new DomainException("Valor do pagamento deve ser maior que zero.");
        if (_items.Count == 0)
            throw new DomainException("Carrinho vazio.");

        decimal change = 0;
        if (method == PaymentMethod.Cash)
        {
            var received = amountReceived ?? amount;
            if (received < amount)
                throw new DomainException("Valor recebido insuficiente.");
            change = decimal.Round(received - amount, 2);
            amountReceived = decimal.Round(received, 2);
        }

        var payment = Payment.Create(Id, method, amount, amountReceived, change);
        _payments.Add(payment);
        MarkUpdated();
        return payment;
    }

    public void Complete()
    {
        EnsureOpen();
        if (_items.Count == 0)
            throw new DomainException("Não é possível concluir venda sem itens.");

        var paid = PaidAmount;
        if (paid < Total)
            throw new DomainException($"Pagamento incompleto. Faltam {Total - paid:C}.");
        if (paid > Total + 0.009m && _payments.All(p => p.Method != PaymentMethod.Cash))
            throw new DomainException("Pagamentos excedem o total da venda.");

        foreach (var payment in _payments)
            payment.Confirm();

        Status = SaleStatus.Completed;
        SoldAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Cancel(Guid cancelledByOperatorId, string reason)
    {
        if (Status != SaleStatus.Completed)
            throw new DomainException("Somente vendas concluídas podem ser canceladas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Motivo do cancelamento é obrigatório.");

        Status = SaleStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledByOperatorId = cancelledByOperatorId;
        CancelledAt = DateTime.UtcNow;

        foreach (var payment in _payments)
            payment.Cancel();

        MarkUpdated();
    }

    public void CancelPayments(IEnumerable<Payment> payments)
    {
        foreach (var payment in payments)
            payment.Cancel();
    }

    public decimal PaidAmount => _payments
        .Where(p => p.Status != PaymentStatus.Cancelled)
        .Sum(p => p.Amount);

    public decimal RemainingAmount => Math.Max(0, Total - PaidAmount);

    public decimal ChangeAmount => _payments
        .Where(p => p.Status != PaymentStatus.Cancelled)
        .Sum(p => p.ChangeAmount);

    private void RecalculateTotals()
    {
        Subtotal = decimal.Round(_items.Sum(i => i.Subtotal), 2);
        Total = decimal.Round(Subtotal - DiscountAmount, 2);
        MarkUpdated();
    }

    private SaleItem GetItem(Guid saleItemId)
    {
        return _items.FirstOrDefault(i => i.Id == saleItemId)
            ?? throw new DomainException("Item da venda não encontrado.");
    }

    private void EnsureOpen()
    {
        if (Status != SaleStatus.Open)
            throw new DomainException("Venda não está aberta para edição.");
    }
}
