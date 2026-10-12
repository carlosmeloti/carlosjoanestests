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

    [Fact]
    public void RemoveMovement_Drops_It_While_Register_Is_Open()
    {
        var cash = CashRegister.Open(Guid.NewGuid(), Guid.NewGuid(), 100m);
        var withdrawal = cash.AddMovement(Guid.NewGuid(), CashMovementType.Withdrawal, 30m, "Sangria parcial");

        cash.RemoveMovement(withdrawal);

        Assert.DoesNotContain(withdrawal, cash.Movements);
        Assert.True(cash.IsOpen);
    }

    [Fact]
    public void RemoveMovement_Rejects_Closed_Register()
    {
        var cash = CashRegister.Open(Guid.NewGuid(), Guid.NewGuid(), 100m);
        var supply = cash.AddMovement(Guid.NewGuid(), CashMovementType.Supply, 20m, "Troco");
        cash.Close(Guid.NewGuid(), countedAmount: 120m, cashSales: 0m, supplies: 20m, withdrawals: 0m);

        Assert.Throws<AcaiPos.Domain.Common.DomainException>(() => cash.RemoveMovement(supply));
        Assert.Contains(supply, cash.Movements);
    }

    [Fact]
    public void RemoveMovement_Rejects_Movement_From_Another_Register()
    {
        var cash = CashRegister.Open(Guid.NewGuid(), Guid.NewGuid(), 100m);
        var other = CashRegister.Open(Guid.NewGuid(), Guid.NewGuid(), 50m);
        var withdrawal = other.AddMovement(Guid.NewGuid(), CashMovementType.Withdrawal, 10m, "Outro caixa");

        Assert.Throws<AcaiPos.Domain.Common.DomainException>(() => cash.RemoveMovement(withdrawal));
    }
}
