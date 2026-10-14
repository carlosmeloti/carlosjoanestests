using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class Payment : Entity
{
    public Guid SaleId { get; private set; }
    public int FormaPagamentoId { get; private set; }
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
        int formaPagamentoId,
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

    public void Confirm()
    {
        Status = PaymentStatus.Confirmed;
    }

    public void Cancel()
    {
        Status = PaymentStatus.Cancelled;
    }
}
