using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Tests;

public class CashRegisterTests
{
    [Fact]
    public void Supply_And_Withdrawal_Affect_Expected_Cash()
    {
        var cash = CashRegister.Open(Guid.NewGuid(), Guid.NewGuid(), 100m);
        cash.AddMovement(Guid.NewGuid(), CashMovementType.Supply, 50m, "Troco extra");
        cash.AddMovement(Guid.NewGuid(), CashMovementType.Withdrawal, 30m, "Sangria parcial");

        cash.Close(Guid.NewGuid(), countedAmount: 200m, cashSales: 80m, supplies: 50m, withdrawals: 30m);

        // 100 + 80 + 50 - 30 = 200
        Assert.Equal(200m, cash.ExpectedAmount);
        Assert.Equal(0m, cash.DifferenceAmount);
        Assert.Equal(CashRegisterStatus.Closed, cash.Status);
    }

    [Fact]
    public void Movement_Requires_Reason_And_Positive_Amount()
    {
        var cash = CashRegister.Open(Guid.NewGuid(), Guid.NewGuid(), 50m);

        Assert.Throws<AcaiPos.Domain.Common.DomainException>(() =>
            cash.AddMovement(Guid.NewGuid(), CashMovementType.Supply, 0m, "x"));

        Assert.Throws<AcaiPos.Domain.Common.DomainException>(() =>
            cash.AddMovement(Guid.NewGuid(), CashMovementType.Withdrawal, 10m, " "));
    }
}
