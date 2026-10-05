using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class StockMovement : AuditableEntity
{
    public Guid ProductId { get; private set; }
    public Guid? SaleId { get; private set; }
    public Guid? OperatorId { get; private set; }
    public StockMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal PreviousQuantity { get; private set; }
    public decimal ResultingQuantity { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    public Product? Product { get; private set; }

    private StockMovement()
    {
    }

    public static StockMovement Create(
        Product product,
        StockMovementType type,
        decimal quantityDelta,
        string reason,
        Guid? saleId = null,
        Guid? operatorId = null)
    {
        if (quantityDelta == 0)
            throw new DomainException("Quantidade da movimentação não pode ser zero.");

        var previous = product.StockQuantity;
        product.AdjustStock(quantityDelta);

        return new StockMovement
        {
            ProductId = product.Id,
            SaleId = saleId,
            OperatorId = operatorId,
            Type = type,
            Quantity = quantityDelta,
            PreviousQuantity = previous,
            ResultingQuantity = product.StockQuantity,
            Reason = reason.Trim(),
            OccurredAt = DateTime.UtcNow
        };
    }
}
