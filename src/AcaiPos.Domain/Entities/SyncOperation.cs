using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class SyncOperation : Entity
{
    public SyncAggregateType AggregateType { get; private set; }
    public Guid AggregateId { get; private set; }
    public SyncOperationKind Kind { get; private set; }
    public SyncStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SyncOperation()
    {
    }

    public static SyncOperation Register(
        ICollection<SyncOperation> operations,
        SyncAggregateType aggregateType,
        Guid aggregateId,
        SyncOperationKind kind)
    {
        if (aggregateId == Guid.Empty)
            throw new DomainException("Agregado da sincronização é obrigatório.");

        var current = operations.FirstOrDefault(item =>
            item.AggregateType == aggregateType && item.AggregateId == aggregateId);

        if (current is null)
        {
            current = new SyncOperation
            {
                AggregateType = aggregateType,
                AggregateId = aggregateId,
                Kind = kind,
                Status = SyncStatus.Pending,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            operations.Add(current);
            return current;
        }

        current.MarkPending(kind);
        return current;
    }

    public void MarkPending(SyncOperationKind kind)
    {
        Kind = kind;
        Status = SyncStatus.Pending;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
