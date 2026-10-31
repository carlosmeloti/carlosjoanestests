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

    public const int LimiteTentativas = 5;

    public void MarkPending(SyncOperationKind kind)
    {
        Kind = kind;
        Status = SyncStatus.Pending;
        LastError = null;
        AttemptCount = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarcarSincronizada()
    {
        Status = SyncStatus.Synced;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RegistrarFalha(string mensagem)
    {
        LastError = Mensagem(mensagem);
        AttemptCount++;
        Status = AttemptCount >= LimiteTentativas ? SyncStatus.Error : SyncStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RegistrarRecusa(string mensagem)
    {
        LastError = Mensagem(mensagem);
        Status = SyncStatus.Error;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string Mensagem(string mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            throw new DomainException("Mensagem do erro de sincronização é obrigatória.");

        var texto = mensagem.Trim();
        return texto.Length <= 2000 ? texto : texto[..2000];
    }
}
