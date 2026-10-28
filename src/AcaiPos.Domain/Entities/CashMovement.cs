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

    public static CashMovement Restaurar(
        Guid id,
        Guid cashRegisterId,
        Guid terminalId,
        Guid operatorId,
        CashMovementType type,
        decimal amount,
        string reason,
        DateTime occurredAt,
        DateTime createdAt,
        DateTime updatedAt,
        DateTime? deletedAt)
    {
        var movement = new CashMovement();
        movement.DefinirId(id);
        movement.AtribuirEspelho(
            cashRegisterId,
            terminalId,
            operatorId,
            type,
            amount,
            reason,
            occurredAt,
            createdAt,
            updatedAt,
            deletedAt);
        return movement;
    }

    public void AplicarEspelho(
        Guid cashRegisterId,
        Guid terminalId,
        Guid operatorId,
        CashMovementType type,
        decimal amount,
        string reason,
        DateTime occurredAt,
        DateTime createdAt,
        DateTime updatedAt,
        DateTime? deletedAt)
        => AtribuirEspelho(
            cashRegisterId,
            terminalId,
            operatorId,
            type,
            amount,
            reason,
            occurredAt,
            createdAt,
            updatedAt,
            deletedAt);

    private void AtribuirEspelho(
        Guid cashRegisterId,
        Guid terminalId,
        Guid operatorId,
        CashMovementType type,
        decimal amount,
        string reason,
        DateTime occurredAt,
        DateTime createdAt,
        DateTime updatedAt,
        DateTime? deletedAt)
    {
        if (cashRegisterId == Guid.Empty)
            throw new DomainException("Caixa é obrigatório.");
        if (terminalId == Guid.Empty)
            throw new DomainException("Terminal é obrigatório.");
        if (operatorId == Guid.Empty)
            throw new DomainException("Operador é obrigatório.");
        if (amount <= 0)
            throw new DomainException("Valor da movimentação deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Motivo da movimentação é obrigatório.");

        CashRegisterId = cashRegisterId;
        TerminalId = terminalId;
        OperatorId = operatorId;
        Type = type;
        Amount = decimal.Round(amount, 2);
        Reason = reason.Trim();
        OccurredAt = occurredAt;
        RestaurarTrilha(createdAt, updatedAt, deletedAt, SyncStatus.Synced);
    }
}
