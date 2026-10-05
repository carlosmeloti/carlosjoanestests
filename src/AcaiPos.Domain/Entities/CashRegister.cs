using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class CashRegister : AuditableEntity
{
    public Guid TerminalId { get; private set; }
    public Guid OperatorId { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public decimal OpeningAmount { get; private set; }
    public decimal? CountedAmount { get; private set; }
    public decimal? ExpectedAmount { get; private set; }
    public decimal? DifferenceAmount { get; private set; }
    public CashRegisterStatus Status { get; private set; }
    public Guid? ClosedByOperatorId { get; private set; }
    public string? ClosingNotes { get; private set; }

    public Terminal? Terminal { get; private set; }
    public User? Operator { get; private set; }

    private readonly List<CashMovement> _movements = [];
    public IReadOnlyCollection<CashMovement> Movements => _movements.AsReadOnly();

    private CashRegister()
    {
    }

    public static CashRegister Open(Guid terminalId, Guid operatorId, decimal openingAmount)
    {
        if (terminalId == Guid.Empty)
            throw new DomainException("Terminal é obrigatório.");
        if (operatorId == Guid.Empty)
            throw new DomainException("Operador é obrigatório.");
        if (openingAmount < 0)
            throw new DomainException("Valor de abertura não pode ser negativo.");

        return new CashRegister
        {
            TerminalId = terminalId,
            OperatorId = operatorId,
            OpenedAt = DateTime.UtcNow,
            OpeningAmount = decimal.Round(openingAmount, 2),
            Status = CashRegisterStatus.Open
        };
    }

    public CashMovement AddMovement(Guid operatorId, CashMovementType type, decimal amount, string reason)
    {
        EnsureOpen();
        if (amount <= 0)
            throw new DomainException("Valor da movimentação deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Motivo da movimentação é obrigatório.");

        var movement = CashMovement.Create(Id, TerminalId, operatorId, type, amount, reason);
        _movements.Add(movement);
        MarkUpdated();
        return movement;
    }

    public void Close(
        Guid closedByOperatorId,
        decimal countedAmount,
        decimal cashSales,
        decimal supplies,
        decimal withdrawals,
        string? notes = null)
    {
        EnsureOpen();
        if (countedAmount < 0)
            throw new DomainException("Valor contado não pode ser negativo.");

        var expected = OpeningAmount + cashSales + supplies - withdrawals;
        ExpectedAmount = decimal.Round(expected, 2);
        CountedAmount = decimal.Round(countedAmount, 2);
        DifferenceAmount = CountedAmount - ExpectedAmount;
        ClosedAt = DateTime.UtcNow;
        ClosedByOperatorId = closedByOperatorId;
        ClosingNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = CashRegisterStatus.Closed;
        MarkUpdated();
    }

    public bool IsOpen => Status == CashRegisterStatus.Open;

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new DomainException("Caixa não está aberto.");
    }
}
