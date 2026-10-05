using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class SaleItem : Entity
{
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ProductSku { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal Subtotal { get; private set; }

    public Product? Product { get; private set; }

    private SaleItem()
    {
    }

    public static SaleItem Create(Guid saleId, Product product, decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");

        var item = new SaleItem
        {
            SaleId = saleId,
            ProductId = product.Id,
            ProductName = product.Name,
            ProductSku = product.Sku,
            Quantity = quantity,
            UnitPrice = product.SalePrice,
            DiscountAmount = 0
        };

        item.Recalculate();
        return item;
    }

    public void IncreaseQuantity(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");

        Quantity += quantity;
        Recalculate();
    }

    public void SetQuantity(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");

        Quantity = quantity;
        Recalculate();
    }

    private void Recalculate()
    {
        Subtotal = decimal.Round((Quantity * UnitPrice) - DiscountAmount, 2);
    }
}
