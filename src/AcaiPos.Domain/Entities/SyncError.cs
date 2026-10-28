using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class SyncError : Entity
{
    public Guid SyncOperationId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public int? HttpStatus { get; private set; }
    public string Message { get; private set; } = string.Empty;

    private SyncError()
    {
    }

    public static SyncError Create(Guid syncOperationId, string message, int? httpStatus = null)
    {
        if (syncOperationId == Guid.Empty)
            throw new DomainException("Operação de sincronização é obrigatória.");
        if (string.IsNullOrWhiteSpace(message))
            throw new DomainException("Mensagem do erro de sincronização é obrigatória.");

        return new SyncError
        {
            SyncOperationId = syncOperationId,
            OccurredAt = DateTime.UtcNow,
            HttpStatus = httpStatus,
            Message = message.Trim()
        };
    }
}
