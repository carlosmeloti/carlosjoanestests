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
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    IQueryable<User> IPosDbContext.Users => Users;
    IQueryable<Store> IPosDbContext.Stores => Stores;
    IQueryable<Terminal> IPosDbContext.Terminals => Terminals;
    IQueryable<Category> IPosDbContext.Categories => Categories;
    IQueryable<Product> IPosDbContext.Products => Products;
    IQueryable<CashRegister> IPosDbContext.CashRegisters => CashRegisters;
    IQueryable<CashMovement> IPosDbContext.CashMovements => CashMovements;
    IQueryable<Sale> IPosDbContext.Sales => Sales;
    IQueryable<SaleItem> IPosDbContext.SaleItems => SaleItems;
    IQueryable<Payment> IPosDbContext.Payments => Payments;
    IQueryable<StockMovement> IPosDbContext.StockMovements => StockMovements;
    IQueryable<AuditEntry> IPosDbContext.AuditEntries => AuditEntries;

    public new void Add<T>(T entity) where T : class => Set<T>().Add(entity);

    public new void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
