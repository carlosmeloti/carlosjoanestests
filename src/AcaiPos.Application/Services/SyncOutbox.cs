using AcaiPos.Application.Abstractions;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Services;

public static class SyncOutbox
{
    public static void Enqueue(
        IPosDbContext db,
        SyncAggregateType aggregateType,
        Guid aggregateId,
        SyncOperationKind kind)
    {
        var existing = db.SyncOperations
            .Where(item => item.AggregateType == aggregateType && item.AggregateId == aggregateId)
            .ToList();

        var count = existing.Count;
        var operation = SyncOperation.Register(existing, aggregateType, aggregateId, kind);
        if (existing.Count != count)
            db.Add(operation);
    }
}
