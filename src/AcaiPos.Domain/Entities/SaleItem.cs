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

    public static SaleItem Restaurar(
        Guid id,
        Guid saleId,
        Guid productId,
        string productName,
        string productSku,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal subtotal)
    {
        if (id == Guid.Empty)
            throw new DomainException("Identificador é obrigatório.");
        if (saleId == Guid.Empty)
            throw new DomainException("Venda é obrigatória.");
        if (productId == Guid.Empty)
            throw new DomainException("Produto é obrigatório.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainException("Nome do produto é obrigatório.");
        if (string.IsNullOrWhiteSpace(productSku))
            throw new DomainException("SKU do produto é obrigatório.");
        if (quantity <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");

        var item = new SaleItem
        {
            SaleId = saleId,
            ProductId = productId,
            ProductName = productName.Trim(),
            ProductSku = productSku.Trim(),
            Quantity = quantity,
            UnitPrice = decimal.Round(unitPrice, 2),
            DiscountAmount = decimal.Round(discountAmount, 2),
            Subtotal = decimal.Round(subtotal, 2)
        };
        item.DefinirId(id);
        return item;
    }

    public void CopiarDe(SaleItem origem)
    {
        SaleId = origem.SaleId;
        ProductId = origem.ProductId;
        ProductName = origem.ProductName;
        ProductSku = origem.ProductSku;
        Quantity = origem.Quantity;
        UnitPrice = origem.UnitPrice;
        DiscountAmount = origem.DiscountAmount;
        Subtotal = origem.Subtotal;
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
