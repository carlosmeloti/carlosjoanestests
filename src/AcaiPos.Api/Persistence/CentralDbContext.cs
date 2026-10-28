using AcaiPos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Api.Persistence;

public sealed class CentralDbContext : DbContext
{
    public CentralDbContext(DbContextOptions<CentralDbContext> options) : base(options)
    {
    }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Store>(builder =>
        {
            builder.ToTable("Stores");
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Terminal>(builder =>
        {
            builder.ToTable("Terminals");
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
            builder.Property<string>("SegredoHash").HasMaxLength(500);
            builder.Property<DateTime?>("SegredoEmitidoEm");
            builder.Ignore(x => x.Store);
            builder.HasIndex(x => new { x.StoreId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<Sale>(builder =>
        {
            builder.ToTable("Sales");
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.TicketNumber).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Subtotal).HasPrecision(18, 2);
            builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            builder.Property(x => x.SurchargeAmount).HasPrecision(18, 2);
            builder.Property(x => x.Total).HasPrecision(18, 2);
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.Origin).HasConversion<int>();
            builder.Property(x => x.SyncStatus).HasConversion<int>();
            builder.HasIndex(x => x.TicketNumber).IsUnique();
            builder.Ignore(x => x.CashRegister);
            builder.Ignore(x => x.Operator);
            builder.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Payments)
                .WithOne()
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<SaleItem>(builder =>
        {
            builder.ToTable("SaleItems");
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.ProductName).HasMaxLength(120).IsRequired();
            builder.Property(x => x.ProductSku).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Quantity).HasPrecision(18, 3);
            builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
            builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            builder.Property(x => x.Subtotal).HasPrecision(18, 2);
            builder.Ignore(x => x.Product);
        });

        modelBuilder.Entity<Payment>(builder =>
        {
            builder.ToTable("Payments");
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Descricao).HasMaxLength(80).IsRequired();
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.AmountReceived).HasPrecision(18, 2);
            builder.Property(x => x.ChangeAmount).HasPrecision(18, 2);
            builder.Property(x => x.Status).HasConversion<int>();
        });

        modelBuilder.Entity<CashRegister>(builder =>
        {
            builder.ToTable("CashRegisters");
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.OpeningAmount).HasPrecision(18, 2);
            builder.Property(x => x.CountedAmount).HasPrecision(18, 2);
            builder.Property(x => x.ExpectedAmount).HasPrecision(18, 2);
            builder.Property(x => x.DifferenceAmount).HasPrecision(18, 2);
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.SyncStatus).HasConversion<int>();
            builder.Property(x => x.ClosingNotes).HasMaxLength(500);
            builder.Ignore(x => x.Terminal);
            builder.Ignore(x => x.Operator);
            builder.Ignore(x => x.Movements);
        });

        modelBuilder.Entity<CashMovement>(builder =>
        {
            builder.ToTable("CashMovements");
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();
            builder.Property(x => x.Type).HasConversion<int>();
            builder.Property(x => x.SyncStatus).HasConversion<int>();
        });

        modelBuilder.Entity<StockMovement>(builder =>
        {
            builder.ToTable("StockMovements");
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Quantity).HasPrecision(18, 3);
            builder.Property(x => x.PreviousQuantity).HasPrecision(18, 3);
            builder.Property(x => x.ResultingQuantity).HasPrecision(18, 3);
            builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();
            builder.Property(x => x.Type).HasConversion<int>();
            builder.Property(x => x.SyncStatus).HasConversion<int>();
            builder.Ignore(x => x.Product);
        });
    }
}
