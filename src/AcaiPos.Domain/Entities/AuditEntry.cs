using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class AuditEntry : Entity
{
    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
    public Guid? OperatorId { get; private set; }
    public Guid? TerminalId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string? Details { get; private set; }

    private AuditEntry()
    {
    }

    public static AuditEntry Create(
        string action,
        string entityName,
        Guid? entityId = null,
        Guid? operatorId = null,
        Guid? terminalId = null,
        string? details = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("Ação de auditoria é obrigatória.");

        return new AuditEntry
        {
            Action = action.Trim(),
            EntityName = entityName.Trim(),
            EntityId = entityId,
            OperatorId = operatorId,
            TerminalId = terminalId,
            Details = details,
            OccurredAt = DateTime.UtcNow
        };
    }
}
