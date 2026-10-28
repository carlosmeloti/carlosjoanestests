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
}
