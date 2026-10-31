using System.Data;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Api.Persistence;

public static class CatalogoSchema
{
    public static async Task GarantirAsync(CentralDbContext db, CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        var abriu = connection.State != ConnectionState.Open;
        if (abriu)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var sqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
            if (!await ExisteAsync(connection, sqlite, "Categories", cancellationToken))
            {
                await ExecutarAsync(connection, sqlite ? CategoriasSqlite : CategoriasMysql, cancellationToken);
                await ExecutarAsync(connection, sqlite
                    ? """CREATE UNIQUE INDEX "IX_Categories_LojaId_Name" ON "Categories" ("LojaId", "Name");"""
                    : "SELECT 1;", cancellationToken);
            }
            if (!await ExisteAsync(connection, sqlite, "Products", cancellationToken))
            {
                await ExecutarAsync(connection, sqlite ? ProdutosSqlite : ProdutosMysql, cancellationToken);
                if (sqlite)
                    await ExecutarAsync(connection, """CREATE UNIQUE INDEX "IX_Products_LojaId_Sku" ON "Products" ("LojaId", "Sku");""", cancellationToken);
            }
            if (!await ExisteAsync(connection, sqlite, "ProductCategories", cancellationToken))
                await ExecutarAsync(connection, sqlite ? VinculosSqlite : VinculosMysql, cancellationToken);
            if (!await ExisteAsync(connection, sqlite, "FormasPagamento", cancellationToken))
            {
                await ExecutarAsync(connection, sqlite ? FormasSqlite : FormasMysql, cancellationToken);
                if (sqlite)
                    await ExecutarAsync(connection, """CREATE UNIQUE INDEX "IX_FormasPagamento_LojaId_Descricao" ON "FormasPagamento" ("LojaId", "Descricao");""", cancellationToken);
            }

            await ConverterFormaPagamentoAsync(connection, sqlite, cancellationToken);
            await GarantirGatilhosUpdatedAtAsync(connection, sqlite, cancellationToken);
        }
        finally
        {
            if (abriu)
                await connection.CloseAsync();
        }
    }

    private static async Task ConverterFormaPagamentoAsync(System.Data.Common.DbConnection connection, bool sqlite, CancellationToken cancellationToken)
    {
        if (!await ExisteAsync(connection, sqlite, "Payments", cancellationToken))
            return;

        if (!await ColunaInteiraAsync(connection, sqlite, cancellationToken))
            return;

        if (sqlite)
        {
            await ExecutarAsync(connection, """
                CREATE TABLE "Payments__guid" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Payments" PRIMARY KEY,
                    "SaleId" TEXT NOT NULL,
                    "FormaPagamentoId" TEXT NOT NULL,
                    "Descricao" TEXT NOT NULL,
                    "Amount" TEXT NOT NULL,
                    "AmountReceived" TEXT NULL,
                    "ChangeAmount" TEXT NOT NULL,
                    "ExternalReference" TEXT NULL,
                    "Status" INTEGER NOT NULL,
                    "PaidAt" TEXT NOT NULL
                );
                """, cancellationToken);
            await ExecutarAsync(connection, """
                INSERT INTO "Payments__guid"
                SELECT "Id", "SaleId",
                    CASE "FormaPagamentoId"
                        WHEN 1 THEN '00000000-0000-4000-8000-000000000001'
                        WHEN 2 THEN '00000000-0000-4000-8000-000000000002'
                        WHEN 3 THEN '00000000-0000-4000-8000-000000000003'
                        WHEN 4 THEN '00000000-0000-4000-8000-000000000004'
                        ELSE '00000000-0000-4000-8000-000000000004'
                    END,
                    "Descricao", "Amount", "AmountReceived", "ChangeAmount", "ExternalReference", "Status", "PaidAt"
                FROM "Payments";
                """, cancellationToken);
            await ExecutarAsync(connection, """DROP TABLE "Payments";""", cancellationToken);
            await ExecutarAsync(connection, """ALTER TABLE "Payments__guid" RENAME TO "Payments";""", cancellationToken);
            return;
        }

        await ExecutarAsync(connection, """
            ALTER TABLE Payments ADD COLUMN FormaPagamentoGuid char(36) NULL;
            """, cancellationToken);
        await ExecutarAsync(connection, """
            UPDATE Payments SET FormaPagamentoGuid = CASE FormaPagamentoId
                WHEN 1 THEN '00000000-0000-4000-8000-000000000001'
                WHEN 2 THEN '00000000-0000-4000-8000-000000000002'
                WHEN 3 THEN '00000000-0000-4000-8000-000000000003'
                WHEN 4 THEN '00000000-0000-4000-8000-000000000004'
                ELSE '00000000-0000-4000-8000-000000000004'
            END;
            """, cancellationToken);
        await ExecutarAsync(connection, "ALTER TABLE Payments DROP COLUMN FormaPagamentoId;", cancellationToken);
        await ExecutarAsync(connection, "ALTER TABLE Payments CHANGE FormaPagamentoGuid FormaPagamentoId char(36) NOT NULL;", cancellationToken);
    }

    private static async Task<bool> ColunaInteiraAsync(System.Data.Common.DbConnection connection, bool sqlite, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sqlite
            ? "SELECT type FROM pragma_table_info('Payments') WHERE name = 'FormaPagamentoId' LIMIT 1;"
            : """
              SELECT DATA_TYPE FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Payments' AND COLUMN_NAME = 'FormaPagamentoId'
              LIMIT 1;
              """;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        var tipo = result?.ToString() ?? string.Empty;
        return tipo.Contains("int", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> ExisteAsync(System.Data.Common.DbConnection connection, bool sqlite, string tabela, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sqlite
            ? "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;"
            : "SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @name LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = sqlite ? "$name" : "@name";
        parameter.Value = tabela;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null and not DBNull;
    }

    private static async Task ExecutarAsync(System.Data.Common.DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task GarantirGatilhosUpdatedAtAsync(
        System.Data.Common.DbConnection connection,
        bool sqlite,
        CancellationToken cancellationToken)
    {
        foreach (var tabela in new[] { "Products", "Categories", "FormasPagamento" })
        {
            var gatilho = "trg_" + tabela + "_UpdatedAt";
            if (sqlite)
            {
                await ExecutarAsync(connection, $"""DROP TRIGGER IF EXISTS "{gatilho}";""", cancellationToken);
                await ExecutarAsync(connection, $"""
                    CREATE TRIGGER "{gatilho}"
                    AFTER UPDATE ON "{tabela}"
                    FOR EACH ROW
                    WHEN NEW."UpdatedAt" = OLD."UpdatedAt"
                    BEGIN
                        UPDATE "{tabela}"
                        SET "UpdatedAt" = strftime('%Y-%m-%d %H:%M:%f', 'now')
                        WHERE "Id" = NEW."Id";
                    END;
                    """, cancellationToken);
                continue;
            }

            await ExecutarAsync(connection, $"DROP TRIGGER IF EXISTS {gatilho};", cancellationToken);
            await ExecutarAsync(connection, $"""
                CREATE TRIGGER {gatilho}
                BEFORE UPDATE ON {tabela}
                FOR EACH ROW
                SET NEW.UpdatedAt = IF(NEW.UpdatedAt = OLD.UpdatedAt, UTC_TIMESTAMP(6), NEW.UpdatedAt);
                """, cancellationToken);
        }
    }

    private const string CategoriasSqlite = """
        CREATE TABLE "Categories" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Categories" PRIMARY KEY,
            "LojaId" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "Description" TEXT NULL,
            "IsActive" INTEGER NOT NULL,
            "IsDefault" INTEGER NOT NULL,
            "SortOrder" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            "DeletedAt" TEXT NULL,
            "SyncStatus" INTEGER NOT NULL
        );
        """;

    private const string ProdutosSqlite = """
        CREATE TABLE "Products" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY,
            "LojaId" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "Sku" TEXT NOT NULL,
            "Barcode" TEXT NULL,
            "SalePrice" TEXT NOT NULL,
            "CostPrice" TEXT NULL,
            "StockQuantity" TEXT NOT NULL,
            "UnitOfMeasure" TEXT NOT NULL,
            "Ncm" TEXT NOT NULL,
            "Cfop" TEXT NOT NULL,
            "Origem" INTEGER NOT NULL,
            "Csosn" TEXT NOT NULL,
            "Cest" TEXT NULL,
            "PisCst" TEXT NOT NULL,
            "CofinsCst" TEXT NOT NULL,
            "IbsCbsCst" TEXT NOT NULL,
            "CClassTrib" TEXT NOT NULL,
            "IsActive" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            "DeletedAt" TEXT NULL,
            "SyncStatus" INTEGER NOT NULL
        );
        """;

    private const string VinculosSqlite = """
        CREATE TABLE "ProductCategories" (
            "ProductId" TEXT NOT NULL,
            "CategoryId" TEXT NOT NULL,
            CONSTRAINT "PK_ProductCategories" PRIMARY KEY ("ProductId", "CategoryId")
        );
        """;

    private const string FormasSqlite = """
        CREATE TABLE "FormasPagamento" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_FormasPagamento" PRIMARY KEY,
            "LojaId" TEXT NOT NULL,
            "Descricao" TEXT NOT NULL,
            "CalculaTroco" INTEGER NOT NULL,
            "IsParcela" INTEGER NOT NULL,
            "CorRgbBotao" TEXT NOT NULL,
            "Ordem" INTEGER NOT NULL,
            "IsEnable" INTEGER NOT NULL,
            "CodigoTpag" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            "DeletedAt" TEXT NULL
        );
        """;

    private const string CategoriasMysql = """
        CREATE TABLE Categories (
            Id char(36) NOT NULL,
            LojaId char(36) NOT NULL,
            Name varchar(80) NOT NULL,
            Description varchar(250) NULL,
            IsActive tinyint(1) NOT NULL,
            IsDefault tinyint(1) NOT NULL,
            SortOrder int NOT NULL,
            CreatedAt datetime(6) NOT NULL,
            UpdatedAt datetime(6) NOT NULL,
            DeletedAt datetime(6) NULL,
            SyncStatus int NOT NULL,
            PRIMARY KEY (Id),
            UNIQUE KEY IX_Categories_LojaId_Name (LojaId, Name)
        );
        """;

    private const string ProdutosMysql = """
        CREATE TABLE Products (
            Id char(36) NOT NULL,
            LojaId char(36) NOT NULL,
            Name varchar(120) NOT NULL,
            Sku varchar(40) NOT NULL,
            Barcode varchar(60) NULL,
            SalePrice decimal(18,2) NOT NULL,
            CostPrice decimal(18,2) NULL,
            StockQuantity decimal(18,3) NOT NULL,
            UnitOfMeasure varchar(10) NOT NULL,
            Ncm varchar(8) NOT NULL,
            Cfop varchar(4) NOT NULL,
            Origem int NOT NULL,
            Csosn varchar(3) NOT NULL,
            Cest varchar(7) NULL,
            PisCst varchar(2) NOT NULL,
            CofinsCst varchar(2) NOT NULL,
            IbsCbsCst varchar(3) NOT NULL,
            CClassTrib varchar(6) NOT NULL,
            IsActive tinyint(1) NOT NULL,
            CreatedAt datetime(6) NOT NULL,
            UpdatedAt datetime(6) NOT NULL,
            DeletedAt datetime(6) NULL,
            SyncStatus int NOT NULL,
            PRIMARY KEY (Id),
            UNIQUE KEY IX_Products_LojaId_Sku (LojaId, Sku)
        );
        """;

    private const string VinculosMysql = """
        CREATE TABLE ProductCategories (
            ProductId char(36) NOT NULL,
            CategoryId char(36) NOT NULL,
            PRIMARY KEY (ProductId, CategoryId)
        );
        """;

    private const string FormasMysql = """
        CREATE TABLE FormasPagamento (
            Id char(36) NOT NULL,
            LojaId char(36) NOT NULL,
            Descricao varchar(50) NOT NULL,
            CalculaTroco tinyint(1) NOT NULL,
            IsParcela tinyint(1) NOT NULL,
            CorRgbBotao varchar(7) NOT NULL,
            Ordem int NOT NULL,
            IsEnable tinyint(1) NOT NULL,
            CodigoTpag varchar(2) NOT NULL,
            UpdatedAt datetime(6) NOT NULL,
            DeletedAt datetime(6) NULL,
            PRIMARY KEY (Id),
            UNIQUE KEY IX_FormasPagamento_LojaId_Descricao (LojaId, Descricao)
        );
        """;
}
