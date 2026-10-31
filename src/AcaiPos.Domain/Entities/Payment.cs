using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class Payment : Entity
{
    public Guid SaleId { get; private set; }
    public Guid FormaPagamentoId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal? AmountReceived { get; private set; }
    public decimal ChangeAmount { get; private set; }
    public string? ExternalReference { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime PaidAt { get; private set; }

    private Payment()
    {
    }

    public static Payment Create(
        Guid saleId,
        Guid formaPagamentoId,
        string descricao,
        decimal amount,
        decimal? amountReceived,
        decimal changeAmount,
        string? externalReference = null)
    {
        return new Payment
        {
            SaleId = saleId,
            FormaPagamentoId = formaPagamentoId,
            Descricao = descricao.Trim(),
            Amount = decimal.Round(amount, 2),
            AmountReceived = amountReceived.HasValue ? decimal.Round(amountReceived.Value, 2) : null,
            ChangeAmount = decimal.Round(changeAmount, 2),
            ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim(),
            Status = PaymentStatus.Pending,
            PaidAt = DateTime.UtcNow
        };
    }

    public static Payment Restaurar(
        Guid id,
        Guid saleId,
        Guid formaPagamentoId,
        string descricao,
        decimal amount,
        decimal? amountReceived,
        decimal changeAmount,
        string? externalReference,
        PaymentStatus status,
        DateTime paidAt)
    {
        if (id == Guid.Empty)
            throw new DomainException("Identificador é obrigatório.");
        if (saleId == Guid.Empty)
            throw new DomainException("Venda é obrigatória.");
        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("Descrição do pagamento é obrigatória.");
        if (amount <= 0)
            throw new DomainException("Valor do pagamento deve ser maior que zero.");

        var payment = new Payment
        {
            SaleId = saleId,
            FormaPagamentoId = formaPagamentoId,
            Descricao = descricao.Trim(),
            Amount = decimal.Round(amount, 2),
            AmountReceived = amountReceived.HasValue ? decimal.Round(amountReceived.Value, 2) : null,
            ChangeAmount = decimal.Round(changeAmount, 2),
            ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim(),
            Status = status,
            PaidAt = paidAt
        };
        payment.DefinirId(id);
        return payment;
    }

    public void CopiarDe(Payment origem)
    {
        SaleId = origem.SaleId;
        FormaPagamentoId = origem.FormaPagamentoId;
        Descricao = origem.Descricao;
        Amount = origem.Amount;
        AmountReceived = origem.AmountReceived;
        ChangeAmount = origem.ChangeAmount;
        ExternalReference = origem.ExternalReference;
        Status = origem.Status;
        PaidAt = origem.PaidAt;
    }

    public void Confirm()
    {
        Status = PaymentStatus.Confirmed;
    }

    public void Cancel()
    {
        Status = PaymentStatus.Cancelled;
    }
}
