using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Tests;

public class SaleTests
{
    [Fact]
    public void Complete_Sale_With_Cash_Calculates_Change()
    {
        var category = Category.Create("Bowls");
        var product = Product.Create("Açaí 500ml", "ACAI-500", [category.Id], 25m);
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T20260101-0001");

        sale.AddItem(product, 2);
        sale.AddPayment(FormaPagamento.IdDinheiro, "Dinheiro", true, 50m, 60m);
        sale.Complete();

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(50m, sale.Total);
        Assert.Equal(10m, sale.ChangeAmount);
    }

    [Fact]
    public void Multiple_Payments_Must_Cover_Total()
    {
        var category = Category.Create("Bowls");
        var product = Product.Create("Açaí 500ml", "ACAI-500", [category.Id], 100m);
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T20260101-0002");

        sale.AddItem(product, 1);
        sale.AddPayment(FormaPagamento.IdDinheiro, "Dinheiro", true, 40m, 40m);
        sale.AddPayment(FormaPagamento.IdPix, "PIX", false, 60m);

        sale.Complete();

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(100m, sale.PaidAmount);
    }

    [Fact]
    public void Surcharge_Increases_Total_After_Discount()
    {
        var category = Category.Create("Bowls");
        var product = Product.Create("Açaí 500ml", "ACAI-500", [category.Id], 100m);
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T20260101-0004");

        sale.AddItem(product, 1);
        sale.ApplyDiscount(10m);
        sale.ApplySurcharge(5m);

        Assert.Equal(95m, sale.Total);

        sale.AddPayment(FormaPagamento.IdPix, "PIX", false, 95m);
        sale.Complete();

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(5m, sale.SurchargeAmount);
    }

    [Fact]
    public void Inactive_Product_Cannot_Be_Sold()
    {
        var category = Category.Create("Bowls");
        var product = Product.Create("Açaí 500ml", "ACAI-500", [category.Id], 25m);
        product.Update(product.Name, product.Sku, [category.Id], product.SalePrice, product.Barcode, product.CostPrice, product.UnitOfMeasure, isActive: false);

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T20260101-0003");

        Assert.Throws<AcaiPos.Domain.Common.DomainException>(() => sale.AddItem(product, 1));
    }
}
