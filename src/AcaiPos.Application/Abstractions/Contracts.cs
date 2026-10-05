using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;
using AcaiPos.Application.Models;

namespace AcaiPos.Application.Abstractions;

public interface IPosDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<Store> Stores { get; }
    IQueryable<Terminal> Terminals { get; }
    IQueryable<Category> Categories { get; }
    IQueryable<Product> Products { get; }
    IQueryable<CashRegister> CashRegisters { get; }
    IQueryable<CashMovement> CashMovements { get; }
    IQueryable<Sale> Sales { get; }
    IQueryable<SaleItem> SaleItems { get; }
    IQueryable<Payment> Payments { get; }
    IQueryable<StockMovement> StockMovements { get; }
    IQueryable<AuditEntry> AuditEntries { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string pin);
    bool Verify(string pin, string hash);
}

public interface ITicketNumberGenerator
{
    Task<string> NextAsync(Guid terminalId, CancellationToken cancellationToken = default);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface ICurrentSession
{
    User? CurrentUser { get; }
    Terminal? CurrentTerminal { get; }
    Store? CurrentStore { get; }
    CashRegister? OpenCashRegister { get; }
    bool IsAuthenticated { get; }
    bool HasOpenCashRegister { get; }

    void SetUser(User user);
    void SetTerminal(Terminal terminal, Store store);
    void SetCashRegister(CashRegister? cashRegister);
    void Clear();
}

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        Guid? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}

public interface ITicketFormatter
{
    string Format(TicketDto ticket);
}

public interface ITicketPrinter
{
    Task PrintAsync(TicketDto ticket, CancellationToken cancellationToken = default);
}
