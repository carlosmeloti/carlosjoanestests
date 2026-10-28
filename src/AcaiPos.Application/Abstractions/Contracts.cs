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
    IQueryable<ProductCategory> ProductCategories { get; }

    void LoadProductCategories(Product product);
    IQueryable<CashRegister> CashRegisters { get; }
    IQueryable<CashMovement> CashMovements { get; }
    IQueryable<Sale> Sales { get; }
    IQueryable<SaleItem> SaleItems { get; }
    IQueryable<Payment> Payments { get; }
    IQueryable<FormaPagamento> FormasPagamento { get; }
    IQueryable<ConfiguracaoFiscal> ConfiguracoesFiscais { get; }
    IQueryable<Configuracao> Configuracoes { get; }
    IQueryable<DocumentoFiscal> DocumentosFiscais { get; }
    IQueryable<StockMovement> StockMovements { get; }
    IQueryable<AuditEntry> AuditEntries { get; }
    IQueryable<SyncOperation> SyncOperations { get; }
    IQueryable<SyncError> SyncErrors { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    void DescartarInclusoesPendentes();
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

public interface IProtecaoCertificado
{
    string Proteger(string senha);
    string Revelar(string senhaProtegida);
}

public interface IEmissorNfce
{
    Task<ResultadoEmissaoNfce> ConsultarStatusAsync(CredenciaisEmissao credenciais, CancellationToken cancellationToken = default);
    Task<ResultadoEmissaoNfce> EmitirAsync(PedidoEmissaoNfce pedido, CancellationToken cancellationToken = default);
    Task<ResultadoEmissaoNfce> EmitirContingenciaAsync(PedidoEmissaoNfce pedido, CancellationToken cancellationToken = default);
    Task<ResultadoEmissaoNfce> TransmitirAsync(CredenciaisEmissao credenciais, string xml, CancellationToken cancellationToken = default);
    Task<ResultadoCancelamentoNfce> CancelarAsync(CancelamentoNfce pedido, CancellationToken cancellationToken = default);
    void ValidarIbsCbs(DateTimeOffset emissao, string ibsCbsCst, string cClassTrib);
}
