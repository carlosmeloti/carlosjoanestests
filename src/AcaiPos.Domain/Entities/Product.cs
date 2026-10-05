using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class Product : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public Guid CategoryId { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal? CostPrice { get; private set; }
    public decimal StockQuantity { get; private set; }
    public string UnitOfMeasure { get; private set; } = "UN";
    public bool IsActive { get; private set; } = true;

    public Category? Category { get; private set; }

    private Product()
    {
    }

    public static Product Create(
        string name,
        string sku,
        Guid categoryId,
        decimal salePrice,
        string? barcode = null,
        decimal? costPrice = null,
        decimal stockQuantity = 0,
        string unitOfMeasure = "UN")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do produto é obrigatório.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU é obrigatório.");
        if (categoryId == Guid.Empty)
            throw new DomainException("Categoria é obrigatória.");
        if (salePrice < 0)
            throw new DomainException("Preço de venda não pode ser negativo.");
        if (stockQuantity < 0)
            throw new DomainException("Estoque não pode ser negativo.");

        return new Product
        {
            Name = name.Trim(),
            Sku = sku.Trim().ToUpperInvariant(),
            Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim(),
            CategoryId = categoryId,
            SalePrice = decimal.Round(salePrice, 2),
            CostPrice = costPrice.HasValue ? decimal.Round(costPrice.Value, 2) : null,
            StockQuantity = stockQuantity,
            UnitOfMeasure = string.IsNullOrWhiteSpace(unitOfMeasure) ? "UN" : unitOfMeasure.Trim().ToUpperInvariant(),
            IsActive = true
        };
    }

    public void Update(
        string name,
        string sku,
        Guid categoryId,
        decimal salePrice,
        string? barcode,
        decimal? costPrice,
        string unitOfMeasure,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do produto é obrigatório.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU é obrigatório.");
        if (salePrice < 0)
            throw new DomainException("Preço de venda não pode ser negativo.");

        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        CategoryId = categoryId;
        SalePrice = decimal.Round(salePrice, 2);
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        CostPrice = costPrice.HasValue ? decimal.Round(costPrice.Value, 2) : null;
        UnitOfMeasure = string.IsNullOrWhiteSpace(unitOfMeasure) ? "UN" : unitOfMeasure.Trim().ToUpperInvariant();
        IsActive = isActive;
        MarkUpdated();
    }

    public void AdjustStock(decimal quantityDelta)
    {
        var newQuantity = StockQuantity + quantityDelta;
        if (newQuantity < 0)
            throw new DomainException($"Estoque insuficiente para {Name}.");

        StockQuantity = newQuantity;
        MarkUpdated();
    }
}
