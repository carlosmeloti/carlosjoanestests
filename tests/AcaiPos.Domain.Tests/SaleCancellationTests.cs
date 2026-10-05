using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Tests;

public class SaleCancellationTests
{
    [Fact]
    public void Cancel_Completed_Sale_Requires_Reason_And_Preserves_Record()
    {
        var category = Category.Create("Bowls");
        var product = Product.Create("Açaí 500ml", "ACAI-500", category.Id, 25m, stockQuantity: 10);
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T20261005-0099");
        sale.AddItem(product, 2);
        sale.AddPayment(PaymentMethod.Pix, 50m);
        sale.Complete();

        var managerId = Guid.NewGuid();
        sale.Cancel(managerId, "Cliente desistiu");

        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal("Cliente desistiu", sale.CancellationReason);
        Assert.Equal(managerId, sale.CancelledByOperatorId);
        Assert.NotNull(sale.CancelledAt);
    }

    [Fact]
    public void Cancel_Without_Reason_Fails()
    {
        var category = Category.Create("Bowls");
        var product = Product.Create("Açaí 500ml", "ACAI-500", category.Id, 25m);
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T20261005-0100");
        sale.AddItem(product, 1);
        sale.AddPayment(PaymentMethod.Pix, 25m);
        sale.Complete();

        Assert.Throws<AcaiPos.Domain.Common.DomainException>(() =>
            sale.Cancel(Guid.NewGuid(), " "));
    }
}
