using AcaiPos.Application.Abstractions;
using AcaiPos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Infrastructure.Persistence;

public sealed class PosDbContext : DbContext, IPosDbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FormaPagamento> FormasPagamento => Set<FormaPagamento>();
    public DbSet<ConfiguracaoFiscal> ConfiguracoesFiscais => Set<ConfiguracaoFiscal>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();
    public DbSet<DocumentoFiscal> DocumentosFiscais => Set<DocumentoFiscal>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SyncOperation> SyncOperations => Set<SyncOperation>();
    public DbSet<SyncError> SyncErrors => Set<SyncError>();
    public DbSet<InscricaoTerminal> InscricoesTerminais => Set<InscricaoTerminal>();

    IQueryable<User> IPosDbContext.Users => Users;
    IQueryable<Store> IPosDbContext.Stores => Stores;
    IQueryable<Terminal> IPosDbContext.Terminals => Terminals;
    IQueryable<Category> IPosDbContext.Categories => Categories;
    IQueryable<Product> IPosDbContext.Products => Products;
    IQueryable<ProductCategory> IPosDbContext.ProductCategories => ProductCategories;
    IQueryable<CashRegister> IPosDbContext.CashRegisters => CashRegisters;
    IQueryable<CashMovement> IPosDbContext.CashMovements => CashMovements;
    IQueryable<Sale> IPosDbContext.Sales => Sales;
    IQueryable<SaleItem> IPosDbContext.SaleItems => SaleItems;
    IQueryable<Payment> IPosDbContext.Payments => Payments;
    IQueryable<FormaPagamento> IPosDbContext.FormasPagamento => FormasPagamento;
    IQueryable<ConfiguracaoFiscal> IPosDbContext.ConfiguracoesFiscais => ConfiguracoesFiscais;
    IQueryable<Configuracao> IPosDbContext.Configuracoes => Configuracoes;
    IQueryable<DocumentoFiscal> IPosDbContext.DocumentosFiscais => DocumentosFiscais;
    IQueryable<StockMovement> IPosDbContext.StockMovements => StockMovements;
    IQueryable<AuditEntry> IPosDbContext.AuditEntries => AuditEntries;
    IQueryable<SyncOperation> IPosDbContext.SyncOperations => SyncOperations;
    IQueryable<SyncError> IPosDbContext.SyncErrors => SyncErrors;
    IQueryable<InscricaoTerminal> IPosDbContext.InscricoesTerminais => InscricoesTerminais;

    public void LoadProductCategories(Product product)
        => Entry(product).Collection(p => p.Categories).Load();

    public new void Add<T>(T entity) where T : class => Set<T>().Add(entity);

    public new void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);

    public void DescartarInclusoesPendentes()
    {
        var adicionados = ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added).ToList();
        foreach (var entry in adicionados.Where(entry => entry.Entity is not Sale))
            entry.State = EntityState.Detached;
        foreach (var entry in adicionados.Where(entry => entry.Entity is Sale))
            entry.State = EntityState.Detached;

        var produtos = ChangeTracker.Entries<Product>().Where(entry => entry.State == EntityState.Modified).ToList();
        foreach (var entry in produtos)
            entry.Reload();
    }

    public void RecarregarCatalogoLocal()
    {
        RecarregarRastreados<Product>();
        RecarregarRastreados<Category>();
        RecarregarRastreados<FormaPagamento>();
        foreach (var entry in ChangeTracker.Entries<ProductCategory>().ToList())
            entry.State = EntityState.Detached;
    }

    private void RecarregarRastreados<T>() where T : class
    {
        foreach (var entry in ChangeTracker.Entries<T>().ToList())
        {
            if (entry.State == EntityState.Added)
                continue;

            try
            {
                entry.Reload();
            }
            catch (InvalidOperationException)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
