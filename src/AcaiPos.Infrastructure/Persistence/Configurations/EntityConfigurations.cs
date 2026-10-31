using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcaiPos.Infrastructure.Persistence.Configurations;

internal static class AuditableExtensions
{
    public static void ConfigureAuditable<T>(this EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.SyncStatus).HasConversion<int>().IsRequired();
        builder.HasIndex(x => x.SyncStatus);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.ConfigureAuditable();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Username).HasMaxLength(60).IsRequired();
        builder.Property(x => x.PinHash).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Role).HasConversion<int>();
        builder.HasIndex(x => x.Username).IsUnique();
    }
}

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores");
        builder.ConfigureAuditable();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.ToTable("Terminals");
        builder.ConfigureAuditable();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.ConfigureAuditable();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(250);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.ConfigureAuditable();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(60);
        builder.Property(x => x.SalePrice).HasPrecision(18, 2);
        builder.Property(x => x.CostPrice).HasPrecision(18, 2);
        builder.Property(x => x.StockQuantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Ncm).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Cfop).HasMaxLength(4).IsRequired();
        builder.Property(x => x.Csosn).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Cest).HasMaxLength(7);
        builder.Property(x => x.PisCst).HasMaxLength(2).IsRequired();
        builder.Property(x => x.CofinsCst).HasMaxLength(2).IsRequired();
        builder.Property(x => x.IbsCbsCst).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CClassTrib).HasMaxLength(6).IsRequired();
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasIndex(x => x.Barcode);
        builder.HasMany(x => x.Categories)
            .WithOne()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Categories).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories");
        builder.HasKey(x => new { x.ProductId, x.CategoryId });
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> builder)
    {
        builder.ToTable("CashRegisters");
        builder.ConfigureAuditable();
        builder.Property(x => x.OpeningAmount).HasPrecision(18, 2);
        builder.Property(x => x.CountedAmount).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedAmount).HasPrecision(18, 2);
        builder.Property(x => x.DifferenceAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ClosingNotes).HasMaxLength(500);
        builder.Property(x => x.StoreId).IsRequired();
        builder.HasIndex(x => x.StoreId);
        builder.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Terminal).WithMany().HasForeignKey(x => x.TerminalId);
        builder.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId);
        builder.HasMany(x => x.Movements)
            .WithOne()
            .HasForeignKey(x => x.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.TerminalId, x.Status });
    }
}

internal sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("CashMovements");
        builder.ConfigureAuditable();
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();
    }
}

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.ConfigureAuditable();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.SurchargeAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Origin).HasConversion<int>();
        builder.Property(x => x.StoreId).IsRequired();
        builder.HasIndex(x => x.StoreId);
        builder.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.TicketNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CpfConsumidor).HasMaxLength(11);
        builder.Property(x => x.CancellationReason).HasMaxLength(255);
        builder.HasIndex(x => x.TicketNumber).IsUnique();
        builder.HasOne(x => x.CashRegister).WithMany().HasForeignKey(x => x.CashRegisterId);
        builder.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId);
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
    }
}

internal sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ProductSku).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FormaPagamentoId).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.AmountReceived).HasPrecision(18, 2);
        builder.Property(x => x.ChangeAmount).HasPrecision(18, 2);
        builder.Property(x => x.ExternalReference).HasMaxLength(120);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.ConfigureAuditable();
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.PreviousQuantity).HasPrecision(18, 3);
        builder.Property(x => x.ResultingQuantity).HasPrecision(18, 3);
        builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(1000);
        builder.HasIndex(x => x.OccurredAt);
    }
}

internal sealed class FormaPagamentoConfiguration : IEntityTypeConfiguration<FormaPagamento>
{
    public void Configure(EntityTypeBuilder<FormaPagamento> builder)
    {
        builder.ToTable("FormasPagamento");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Descricao).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CalculaTroco).HasDefaultValue(false);
        builder.Property(x => x.IsParcela).HasDefaultValue(false);
        builder.Property(x => x.IsEnable).HasDefaultValue(true);
        builder.Property(x => x.CorRgbBotao).HasMaxLength(7).IsRequired().HasDefaultValue(FormaPagamento.DefaultButtonColor);
        builder.Property(x => x.Ordem).IsRequired();
        builder.Property(x => x.CodigoTpag).HasMaxLength(2).IsRequired().HasDefaultValue("99");
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}

internal sealed class ConfiguracaoFiscalConfiguration : IEntityTypeConfiguration<ConfiguracaoFiscal>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoFiscal> builder)
    {
        builder.ToTable("ConfiguracoesFiscais");
        builder.ConfigureAuditable();
        builder.Property(x => x.RazaoSocial).HasMaxLength(60).IsRequired();
        builder.Property(x => x.NomeFantasia).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Cnpj).HasMaxLength(14).IsRequired();
        builder.Property(x => x.InscricaoEstadual).HasMaxLength(14).IsRequired();
        builder.Property(x => x.Logradouro).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Numero).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Bairro).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Municipio).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Uf).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Cep).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Telefone).HasMaxLength(14);
        builder.Property(x => x.Csc).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CertificadoArquivo).HasMaxLength(500);
        builder.Property(x => x.CertificadoSenhaProtegida).HasMaxLength(500);
        builder.Property(x => x.JustificativaContingencia).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.StoreId).IsUnique();
    }
}

internal sealed class ConfiguracaoConfiguration : IEntityTypeConfiguration<Configuracao>
{
    public void Configure(EntityTypeBuilder<Configuracao> builder)
    {
        builder.ToTable("Configuracoes");
        builder.ConfigureAuditable();
        builder.Property(x => x.ModoInterface).HasConversion<int>().IsRequired();
        builder.Property(x => x.ExibirBlocosProdutos).IsRequired();
        builder.Property(x => x.ExibirCategorias).IsRequired();
    }
}

internal sealed class DocumentoFiscalConfiguration : IEntityTypeConfiguration<DocumentoFiscal>
{
    public void Configure(EntityTypeBuilder<DocumentoFiscal> builder)
    {
        builder.ToTable("DocumentosFiscais");
        builder.ConfigureAuditable();
        builder.Property(x => x.Chave).HasMaxLength(44);
        builder.Property(x => x.Protocolo).HasMaxLength(20);
        builder.Property(x => x.ProtocoloCancelamento).HasMaxLength(20);
        builder.Property(x => x.CStat).HasMaxLength(10);
        builder.Property(x => x.Motivo).HasMaxLength(500);
        builder.Property(x => x.QrCode).HasMaxLength(1000);
        builder.Property(x => x.UrlChave).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.SaleId);
        builder.HasIndex(x => new { x.Serie, x.Numero });
        builder.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SyncOperationConfiguration : IEntityTypeConfiguration<SyncOperation>
{
    public void Configure(EntityTypeBuilder<SyncOperation> builder)
    {
        builder.ToTable("SyncOperations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AggregateType).HasConversion<int>().IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => new { x.AggregateType, x.AggregateId }).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}

internal sealed class InscricaoTerminalConfiguration : IEntityTypeConfiguration<InscricaoTerminal>
{
    public void Configure(EntityTypeBuilder<InscricaoTerminal> builder)
    {
        builder.ToTable("InscricoesTerminais");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EnderecoApi).HasMaxLength(300).IsRequired();
        builder.Property(x => x.LojaId).IsRequired();
        builder.Property(x => x.TerminalId).IsRequired();
        builder.Property(x => x.SegredoProtegido).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CatalogoLidoEm);
    }
}

internal sealed class SyncErrorConfiguration : IEntityTypeConfiguration<SyncError>
{
    public void Configure(EntityTypeBuilder<SyncError> builder)
    {
        builder.ToTable("SyncErrors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.HasIndex(x => x.SyncOperationId);
    }
}
