using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class CashMovement : AuditableEntity
{
    public Guid CashRegisterId { get; private set; }
    public Guid TerminalId { get; private set; }
    public Guid OperatorId { get; private set; }
    public CashMovementType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    private CashMovement()
    {
    }

    public static CashMovement Create(
        Guid cashRegisterId,
        Guid terminalId,
        Guid operatorId,
        CashMovementType type,
        decimal amount,
        string reason)
    {
        return new CashMovement
        {
            CashRegisterId = cashRegisterId,
            TerminalId = terminalId,
            OperatorId = operatorId,
            Type = type,
            Amount = decimal.Round(amount, 2),
            Reason = reason.Trim(),
            OccurredAt = DateTime.UtcNow
        };
    }
}
