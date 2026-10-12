using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class ProductCategory
{
    public Guid ProductId { get; private set; }
    public Guid CategoryId { get; private set; }

    private ProductCategory()
    {
    }

    public static ProductCategory Create(Guid productId, Guid categoryId)
    {
        if (productId == Guid.Empty || categoryId == Guid.Empty)
            throw new DomainException("Categoria é obrigatória.");

        return new ProductCategory
        {
            ProductId = productId,
            CategoryId = categoryId
        };
    }
}
