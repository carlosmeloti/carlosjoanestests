using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Tests;

public class SyncOperationTests
{
    [Fact]
    public void Complete_And_Cancel_Leave_One_Operation_For_The_Sale()
    {
        var saleId = Guid.NewGuid();
        var queue = new List<SyncOperation>();

        var completed = SyncOperation.Register(queue, SyncAggregateType.Sale, saleId, SyncOperationKind.Upsert);
        var cancelled = SyncOperation.Register(queue, SyncAggregateType.Sale, saleId, SyncOperationKind.Upsert);

        Assert.Single(queue);
        Assert.Same(completed, cancelled);
        Assert.Equal(saleId, cancelled.AggregateId);
        Assert.Equal(SyncAggregateType.Sale, cancelled.AggregateType);
        Assert.Equal(SyncOperationKind.Upsert, cancelled.Kind);
        Assert.Equal(SyncStatus.Pending, cancelled.Status);
    }

    [Fact]
    public void Five_Failures_Become_Error_And_A_New_Change_Resets_The_Counter()
    {
        var queue = new List<SyncOperation>();
        var operation = SyncOperation.Register(queue, SyncAggregateType.Sale, Guid.NewGuid(), SyncOperationKind.Upsert);

        for (var tentativa = 1; tentativa < SyncOperation.LimiteTentativas; tentativa++)
        {
            operation.RegistrarFalha("rede");
            Assert.Equal(SyncStatus.Pending, operation.Status);
            Assert.Equal(tentativa, operation.AttemptCount);
        }

        operation.RegistrarFalha("rede");
        Assert.Equal(SyncStatus.Error, operation.Status);
        Assert.Equal(SyncOperation.LimiteTentativas, operation.AttemptCount);

        operation.MarkPending(SyncOperationKind.Upsert);
        Assert.Equal(SyncStatus.Pending, operation.Status);
        Assert.Equal(0, operation.AttemptCount);
        Assert.Null(operation.LastError);
    }

    [Fact]
    public void Rejection_Stops_Without_Counting_Another_Blind_Retry()
    {
        var queue = new List<SyncOperation>();
        var operation = SyncOperation.Register(queue, SyncAggregateType.CashRegister, Guid.NewGuid(), SyncOperationKind.Upsert);

        operation.RegistrarRecusa("Lote de outra loja.");

        Assert.Equal(SyncStatus.Error, operation.Status);
        Assert.Equal(0, operation.AttemptCount);
        Assert.Equal("Lote de outra loja.", operation.LastError);
    }
}
