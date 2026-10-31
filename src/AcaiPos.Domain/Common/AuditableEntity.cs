namespace AcaiPos.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; protected set; }
    public SyncStatus SyncStatus { get; protected set; } = SyncStatus.Pending;

    public bool IsDeleted => DeletedAt.HasValue;

    public void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
        SyncStatus = SyncStatus.Pending;
    }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void MarkSynced()
    {
        SyncStatus = SyncStatus.Synced;
    }

    public void MarcarFalhaSincronizacao()
    {
        SyncStatus = SyncStatus.Error;
    }

    public void AplicarTrilhaCentral(DateTime updatedAt, DateTime? deletedAt)
    {
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
        SyncStatus = SyncStatus.Synced;
    }

    protected void RestaurarTrilha(DateTime createdAt, DateTime updatedAt, DateTime? deletedAt, SyncStatus syncStatus)
    {
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
        SyncStatus = syncStatus;
    }
}

public enum SyncStatus
{
    Pending = 0,
    Synced = 1,
    Error = 2
}
