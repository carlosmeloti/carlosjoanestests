using System.Data;
using System.Data.Common;
using AcaiPos.Application.Abstractions;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;
using AcaiPos.Infrastructure.Persistence;
using AcaiPos.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AcaiPos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton(new CaminhoBancoLocal(databasePath));
        services.AddSingleton<SincronizadorCatalogo>();
        services.AddSingleton<SincronizadorFila>();
        services.AddDbContext<PosDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"),
            contextLifetime: ServiceLifetime.Singleton,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddScoped<IPosDbContext>(sp => sp.GetRequiredService<PosDbContext>());
        services.AddSingleton<IPasswordHasher, Security.Pbkdf2PasswordHasher>();
        services.AddSingleton<IClock, Services.SystemClock>();
        services.AddSingleton<ITicketNumberGenerator, Services.TicketNumberGenerator>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.EnsureCreatedAsync();
        await MigrateProductCategoriesAsync(db);
        await MigrateSaleSurchargeAsync(db);
        await MigrateFormasPagamentoAsync(db);
        await MigrateFiscalAsync(db);
        await MigrateFormaPagamentoGuidAsync(db);
        await MigrateConfiguracoesAsync(db);
        await MigrateInscricaoTerminalAsync(db);
        await SeedAsync(db, hasher);
    }

    private static async Task MigrateProductCategoriesAsync(PosDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            if (!await HasColumnAsync(connection, "Products", "CategoryId"))
                return;

            await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = OFF;");
            try
            {
                await using var transaction = await connection.BeginTransactionAsync();
                try
                {
                    foreach (var sql in ProductCategoryMigrationSql)
                        await ExecuteNonQueryAsync(connection, sql, transaction);

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;");
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task MigrateSaleSurchargeAsync(PosDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            if (await HasColumnAsync(connection, "Sales", "SurchargeAmount"))
                return;

            await ExecuteNonQueryAsync(
                connection,
                """ALTER TABLE "Sales" ADD COLUMN "SurchargeAmount" TEXT NOT NULL DEFAULT '0.0';""");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task MigrateFormasPagamentoAsync(PosDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            if (!await HasTableAsync(connection, "FormasPagamento"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    CREATE TABLE "FormasPagamento" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_FormasPagamento" PRIMARY KEY AUTOINCREMENT,
                        "Descricao" TEXT NOT NULL,
                        "CalculaTroco" INTEGER NOT NULL DEFAULT 0,
                        "IsParcela" INTEGER NOT NULL DEFAULT 0,
                        "CorRgbBotao" TEXT NOT NULL DEFAULT '#2A3642',
                        "Ordem" INTEGER NOT NULL,
                        "IsEnable" INTEGER NOT NULL DEFAULT 1
                    );
                    """);
            }

            if (await CountAsync(connection, "FormasPagamento") == 0)
            {
                var idInteiro = await ColunaInteiraAsync(connection, "FormasPagamento", "Id");
                await ExecuteNonQueryAsync(
                    connection,
                    idInteiro
                        ? """
                          INSERT INTO "FormasPagamento"
                              ("Id", "Descricao", "CalculaTroco", "IsParcela", "CorRgbBotao", "Ordem", "IsEnable")
                          VALUES
                              (1, 'PIX', 0, 0, '#2F9E7A', 1, 1),
                              (2, 'Débito', 0, 0, '#2A3642', 2, 1),
                              (3, 'Crédito', 0, 0, '#2A3642', 3, 1),
                              (4, 'Dinheiro', 1, 0, '#2A3642', 4, 1);
                          """
                        : """
                          INSERT INTO "FormasPagamento"
                              ("Id", "Descricao", "CalculaTroco", "IsParcela", "CorRgbBotao", "Ordem", "IsEnable")
                          VALUES
                              ('00000000-0000-4000-8000-000000000001', 'PIX', 0, 0, '#2F9E7A', 1, 1),
                              ('00000000-0000-4000-8000-000000000002', 'Débito', 0, 0, '#2A3642', 2, 1),
                              ('00000000-0000-4000-8000-000000000003', 'Crédito', 0, 0, '#2A3642', 3, 1),
                              ('00000000-0000-4000-8000-000000000004', 'Dinheiro', 1, 0, '#2A3642', 4, 1);
                          """);
            }

            if (!await HasColumnAsync(connection, "Payments", "FormaPagamentoId"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """ALTER TABLE "Payments" ADD COLUMN "FormaPagamentoId" INTEGER NOT NULL DEFAULT 0;""");
            }

            if (!await HasColumnAsync(connection, "Payments", "Descricao"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """ALTER TABLE "Payments" ADD COLUMN "Descricao" TEXT NOT NULL DEFAULT '';""");
            }

            if (await HasColumnAsync(connection, "Payments", "Method"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    UPDATE "Payments"
                    SET "FormaPagamentoId" = CASE "Method"
                            WHEN 3 THEN 1
                            WHEN 1 THEN 2
                            WHEN 2 THEN 3
                            WHEN 0 THEN 4
                            ELSE "FormaPagamentoId"
                        END,
                        "Descricao" = CASE "Method"
                            WHEN 3 THEN 'PIX'
                            WHEN 1 THEN 'Débito'
                            WHEN 2 THEN 'Crédito'
                            WHEN 0 THEN 'Dinheiro'
                            ELSE "Descricao"
                        END
                    WHERE "FormaPagamentoId" = 0;
                    """);
            }

            await EnsureLegacyPaymentMethodDefaultAsync(connection);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task MigrateFiscalAsync(PosDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await AddColumnIfMissingAsync(connection, "Products", "Ncm", "TEXT NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync(connection, "Products", "Cfop", "TEXT NOT NULL DEFAULT '5102'");
            await AddColumnIfMissingAsync(connection, "Products", "Origem", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync(connection, "Products", "Csosn", "TEXT NOT NULL DEFAULT '102'");
            await AddColumnIfMissingAsync(connection, "Products", "Cest", "TEXT NULL");
            await AddColumnIfMissingAsync(connection, "Products", "PisCst", "TEXT NOT NULL DEFAULT '49'");
            await AddColumnIfMissingAsync(connection, "Products", "CofinsCst", "TEXT NOT NULL DEFAULT '49'");
            await AddColumnIfMissingAsync(connection, "Products", "IbsCbsCst", "TEXT NOT NULL DEFAULT '000'");
            await AddColumnIfMissingAsync(connection, "Products", "CClassTrib", "TEXT NOT NULL DEFAULT '000001'");
            await AddColumnIfMissingAsync(connection, "Sales", "CpfConsumidor", "TEXT NULL");
            await AddColumnIfMissingAsync(connection, "FormasPagamento", "CodigoTpag", "TEXT NOT NULL DEFAULT '99'");
            await AddColumnIfMissingAsync(connection, "Categories", "IsDefault", "INTEGER NOT NULL DEFAULT 0");

            if (await HasTableAsync(connection, "FormasPagamento"))
            {
                await ExecuteNonQueryAsync(connection, """UPDATE "FormasPagamento" SET "CodigoTpag" = '17' WHERE "Id" = 1 OR "Id" = '00000000-0000-4000-8000-000000000001';""");
                await ExecuteNonQueryAsync(connection, """UPDATE "FormasPagamento" SET "CodigoTpag" = '04' WHERE "Id" = 2 OR "Id" = '00000000-0000-4000-8000-000000000002';""");
                await ExecuteNonQueryAsync(connection, """UPDATE "FormasPagamento" SET "CodigoTpag" = '03' WHERE "Id" = 3 OR "Id" = '00000000-0000-4000-8000-000000000003';""");
                await ExecuteNonQueryAsync(connection, """UPDATE "FormasPagamento" SET "CodigoTpag" = '01' WHERE "Id" = 4 OR "Id" = '00000000-0000-4000-8000-000000000004';""");
            }

            if (!await HasTableAsync(connection, "ConfiguracoesFiscais"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    CREATE TABLE "ConfiguracoesFiscais" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_ConfiguracoesFiscais" PRIMARY KEY,
                        "StoreId" TEXT NOT NULL,
                        "RazaoSocial" TEXT NOT NULL,
                        "NomeFantasia" TEXT NOT NULL,
                        "Cnpj" TEXT NOT NULL,
                        "InscricaoEstadual" TEXT NOT NULL,
                        "Crt" INTEGER NOT NULL,
                        "Logradouro" TEXT NOT NULL,
                        "Numero" TEXT NOT NULL,
                        "Bairro" TEXT NOT NULL,
                        "CodigoMunicipio" INTEGER NOT NULL,
                        "Municipio" TEXT NOT NULL,
                        "Uf" TEXT NOT NULL,
                        "Cep" TEXT NOT NULL,
                        "Telefone" TEXT NULL,
                        "Csc" TEXT NOT NULL,
                        "IdTokenCsc" INTEGER NOT NULL,
                        "Homologacao" INTEGER NOT NULL,
                        "Serie" INTEGER NOT NULL,
                        "ProximoNumero" INTEGER NOT NULL,
                        "CertificadoArquivo" TEXT NULL,
                        "CertificadoSenhaProtegida" TEXT NULL,
                        "JustificativaContingencia" TEXT NOT NULL,
                        "EmitirPorPadrao" INTEGER NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NOT NULL,
                        "DeletedAt" TEXT NULL,
                        "SyncStatus" INTEGER NOT NULL
                    );
                    """);
                await ExecuteNonQueryAsync(
                    connection,
                    """CREATE UNIQUE INDEX "IX_ConfiguracoesFiscais_StoreId" ON "ConfiguracoesFiscais" ("StoreId");""");
            }

            if (!await HasTableAsync(connection, "DocumentosFiscais"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    CREATE TABLE "DocumentosFiscais" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentosFiscais" PRIMARY KEY,
                        "SaleId" TEXT NULL,
                        "Modelo" INTEGER NOT NULL,
                        "Serie" INTEGER NOT NULL,
                        "Numero" INTEGER NOT NULL,
                        "Chave" TEXT NULL,
                        "TipoEmissao" INTEGER NOT NULL,
                        "Status" INTEGER NOT NULL,
                        "Protocolo" TEXT NULL,
                        "CStat" TEXT NULL,
                        "Motivo" TEXT NULL,
                        "QrCode" TEXT NULL,
                        "UrlChave" TEXT NULL,
                        "Xml" TEXT NULL,
                        "ProtocoloCancelamento" TEXT NULL,
                        "XmlEvento" TEXT NULL,
                        "EmitidoEm" TEXT NOT NULL,
                        "AutorizadoEm" TEXT NULL,
                        "CanceladoEm" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NOT NULL,
                        "DeletedAt" TEXT NULL,
                        "SyncStatus" INTEGER NOT NULL,
                        CONSTRAINT "FK_DocumentosFiscais_Sales_SaleId" FOREIGN KEY ("SaleId") REFERENCES "Sales" ("Id") ON DELETE RESTRICT
                    );
                    """);
                await ExecuteNonQueryAsync(
                    connection,
                    """CREATE INDEX "IX_DocumentosFiscais_SaleId" ON "DocumentosFiscais" ("SaleId");""");
                await ExecuteNonQueryAsync(
                    connection,
                    """CREATE INDEX "IX_DocumentosFiscais_Serie_Numero" ON "DocumentosFiscais" ("Serie", "Numero");""");
            }

            await AddColumnIfMissingAsync(connection, "DocumentosFiscais", "ProtocoloCancelamento", "TEXT NULL");
            await AddColumnIfMissingAsync(connection, "DocumentosFiscais", "XmlEvento", "TEXT NULL");
            await AddColumnIfMissingAsync(connection, "DocumentosFiscais", "CanceladoEm", "TEXT NULL");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task AddColumnIfMissingAsync(DbConnection connection, string table, string column, string definition)
    {
        if (!await HasTableAsync(connection, table) || await HasColumnAsync(connection, table, column))
            return;

        await ExecuteNonQueryAsync(connection, $"""ALTER TABLE "{table}" ADD COLUMN "{column}" {definition};""");
    }

    private static async Task EnsureLegacyPaymentMethodDefaultAsync(DbConnection connection)
    {
        if (!await HasColumnAsync(connection, "Payments", "Method"))
            return;

        if (await ColumnHasDefaultAsync(connection, "Payments", "Method"))
            return;

        var columns = await ReadColumnsAsync(connection, "Payments");
        var definitions = columns.Select(column =>
        {
            var definition = $"\"{column.Name}\" {column.Type}";
            if (column.NotNull)
                definition += " NOT NULL";
            if (column.Name == "Method")
                definition += " DEFAULT 0";
            else if (!string.IsNullOrWhiteSpace(column.DefaultSql))
                definition += $" DEFAULT {column.DefaultSql}";
            if (column.PrimaryKey)
                definition += " CONSTRAINT \"PK_Payments\" PRIMARY KEY";
            return definition;
        });

        var foreignKeys = await ReadForeignKeysAsync(connection, "Payments");
        var indexes = await ReadIndexSqlAsync(connection, "Payments");
        var quotedColumns = string.Join(", ", columns.Select(column => $"\"{column.Name}\""));
        var createSql = $"""
            CREATE TABLE "Payments__new" (
                {string.Join(",\n                ", definitions)}{foreignKeys}
            );
            """;
        var insertSql = $"""
            INSERT INTO "Payments__new" ({quotedColumns})
            SELECT {quotedColumns} FROM "Payments";
            """;

        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = OFF;");
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await ExecuteNonQueryAsync(connection, createSql, transaction);
            await ExecuteNonQueryAsync(connection, insertSql, transaction);
            await ExecuteNonQueryAsync(connection, """DROP TABLE "Payments";""", transaction);
            await ExecuteNonQueryAsync(connection, """ALTER TABLE "Payments__new" RENAME TO "Payments";""", transaction);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;");
        }

        foreach (var indexSql in indexes)
            await ExecuteNonQueryAsync(connection, indexSql);
    }

    private static async Task<bool> ColumnHasDefaultAsync(DbConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT dflt_value FROM pragma_table_info('{table}') WHERE name = '{column}' LIMIT 1;";
        var result = await command.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }

    private static async Task<List<SqliteColumn>> ReadColumnsAsync(DbConnection connection, string table)
    {
        var columns = new List<SqliteColumn>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name, type, \"notnull\", dflt_value, pk FROM pragma_table_info('{table}') ORDER BY cid;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new SqliteColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2) == 1,
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4) > 0));
        }

        return columns;
    }

    private static async Task<string> ReadForeignKeysAsync(DbConnection connection, string table)
    {
        var clauses = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"from\", \"table\", \"to\", on_update, on_delete FROM pragma_foreign_key_list('{table}');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var from = reader.GetString(0);
            var target = reader.GetString(1);
            var to = reader.GetString(2);
            var onUpdate = reader.GetString(3);
            var onDelete = reader.GetString(4);
            clauses.Add(
                $"""CONSTRAINT "FK_{table}_{target}_{from}" FOREIGN KEY ("{from}") REFERENCES "{target}" ("{to}") ON UPDATE {onUpdate} ON DELETE {onDelete}""");
        }

        return clauses.Count == 0 ? string.Empty : ",\n                " + string.Join(",\n                ", clauses);
    }

    private static async Task<List<string>> ReadIndexSqlAsync(DbConnection connection, string table)
    {
        var indexes = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'index' AND tbl_name = $table AND sql IS NOT NULL;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$table";
        parameter.Value = table;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
                indexes.Add(reader.GetString(0));
        }

        return indexes;
    }

    private sealed record SqliteColumn(string Name, string Type, bool NotNull, string? DefaultSql, bool PrimaryKey);

    private static async Task<bool> HasTableAsync(DbConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }

    private static async Task<bool> ColunaInteiraAsync(DbConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT type FROM pragma_table_info('{table}') WHERE name = '{column}' LIMIT 1;";
        var result = await command.ExecuteScalarAsync();
        return result is string type && type.Contains("INT", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task MigrateFormaPagamentoGuidAsync(PosDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            if (await HasTableAsync(connection, "FormasPagamento")
                && await ColunaInteiraAsync(connection, "FormasPagamento", "Id"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    CREATE TABLE "FormasPagamento__guid" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_FormasPagamento" PRIMARY KEY,
                        "Descricao" TEXT NOT NULL,
                        "CalculaTroco" INTEGER NOT NULL DEFAULT 0,
                        "IsParcela" INTEGER NOT NULL DEFAULT 0,
                        "CorRgbBotao" TEXT NOT NULL DEFAULT '#2A3642',
                        "Ordem" INTEGER NOT NULL,
                        "IsEnable" INTEGER NOT NULL DEFAULT 1,
                        "CodigoTpag" TEXT NOT NULL DEFAULT '99',
                        "UpdatedAt" TEXT NOT NULL,
                        "DeletedAt" TEXT NULL
                    );
                    """);
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    INSERT INTO "FormasPagamento__guid"
                        ("Id", "Descricao", "CalculaTroco", "IsParcela", "CorRgbBotao", "Ordem", "IsEnable", "CodigoTpag", "UpdatedAt")
                    SELECT
                        CASE "Id"
                            WHEN 1 THEN '00000000-0000-4000-8000-000000000001'
                            WHEN 2 THEN '00000000-0000-4000-8000-000000000002'
                            WHEN 3 THEN '00000000-0000-4000-8000-000000000003'
                            WHEN 4 THEN '00000000-0000-4000-8000-000000000004'
                            ELSE printf('00000000-0000-4000-8000-%012d', "Id")
                        END,
                        "Descricao", "CalculaTroco", "IsParcela", "CorRgbBotao", "Ordem", "IsEnable",
                        COALESCE("CodigoTpag", '99'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                    FROM "FormasPagamento";
                    """);
                await ExecuteNonQueryAsync(connection, """DROP TABLE "FormasPagamento";""");
                await ExecuteNonQueryAsync(connection, """ALTER TABLE "FormasPagamento__guid" RENAME TO "FormasPagamento";""");
            }

            if (await HasTableAsync(connection, "Payments")
                && await ColunaInteiraAsync(connection, "Payments", "FormaPagamentoId"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
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
                    """);
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    INSERT INTO "Payments__guid"
                    SELECT
                        "Id",
                        "SaleId",
                        CASE "FormaPagamentoId"
                            WHEN 1 THEN '00000000-0000-4000-8000-000000000001'
                            WHEN 2 THEN '00000000-0000-4000-8000-000000000002'
                            WHEN 3 THEN '00000000-0000-4000-8000-000000000003'
                            WHEN 4 THEN '00000000-0000-4000-8000-000000000004'
                            ELSE printf('00000000-0000-4000-8000-%012d', "FormaPagamentoId")
                        END,
                        "Descricao",
                        "Amount",
                        "AmountReceived",
                        "ChangeAmount",
                        "ExternalReference",
                        "Status",
                        "PaidAt"
                    FROM "Payments";
                    """);
                await ExecuteNonQueryAsync(connection, """DROP TABLE "Payments";""");
                await ExecuteNonQueryAsync(connection, """ALTER TABLE "Payments__guid" RENAME TO "Payments";""");
            }

            if (await HasTableAsync(connection, "FormasPagamento"))
                await AddColumnIfMissingAsync(connection, "FormasPagamento", "UpdatedAt", "TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task<long> CountAsync(DbConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM \"{table}\";";
        var result = await command.ExecuteScalarAsync();
        return result is long count ? count : Convert.ToInt64(result);
    }

    private static async Task<bool> HasColumnAsync(DbConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT 1 FROM pragma_table_info('{table}') WHERE name = '{column}' LIMIT 1;";
        var result = await command.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }

    private static async Task ExecuteNonQueryAsync(DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (transaction is not null)
            command.Transaction = transaction;

        await command.ExecuteNonQueryAsync();
    }

    private static readonly string[] ProductCategoryMigrationSql =
    [
        """
        CREATE TABLE IF NOT EXISTS "ProductCategories" (
            "ProductId" TEXT NOT NULL,
            "CategoryId" TEXT NOT NULL,
            CONSTRAINT "PK_ProductCategories" PRIMARY KEY ("ProductId", "CategoryId"),
            CONSTRAINT "FK_ProductCategories_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_ProductCategories_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT
        );
        """,
        """
        INSERT OR IGNORE INTO "ProductCategories" ("ProductId", "CategoryId")
        SELECT "Id", "CategoryId" FROM "Products";
        """,
        """
        CREATE TABLE "Products__new" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY,
            "Name" TEXT NOT NULL,
            "Sku" TEXT NOT NULL,
            "Barcode" TEXT NULL,
            "SalePrice" TEXT NOT NULL,
            "CostPrice" TEXT NULL,
            "StockQuantity" TEXT NOT NULL,
            "UnitOfMeasure" TEXT NOT NULL,
            "IsActive" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            "DeletedAt" TEXT NULL,
            "SyncStatus" INTEGER NOT NULL
        );
        """,
        """
        INSERT INTO "Products__new" (
            "Id", "Name", "Sku", "Barcode", "SalePrice", "CostPrice", "StockQuantity",
            "UnitOfMeasure", "IsActive", "CreatedAt", "UpdatedAt", "DeletedAt", "SyncStatus")
        SELECT
            "Id", "Name", "Sku", "Barcode", "SalePrice", "CostPrice", "StockQuantity",
            "UnitOfMeasure", "IsActive", "CreatedAt", "UpdatedAt", "DeletedAt", "SyncStatus"
        FROM "Products";
        """,
        """DROP TABLE "Products";""",
        """ALTER TABLE "Products__new" RENAME TO "Products";""",
        """CREATE INDEX "IX_Products_Barcode" ON "Products" ("Barcode");""",
        """CREATE UNIQUE INDEX "IX_Products_Sku" ON "Products" ("Sku");""",
        """CREATE INDEX "IX_Products_SyncStatus" ON "Products" ("SyncStatus");""",
        """CREATE INDEX "IX_ProductCategories_CategoryId" ON "ProductCategories" ("CategoryId");"""
    ];

    private static async Task MigrateConfiguracoesAsync(PosDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            if (!await HasTableAsync(connection, "Configuracoes"))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    CREATE TABLE "Configuracoes" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_Configuracoes" PRIMARY KEY,
                        "ModoInterface" INTEGER NOT NULL,
                        "ExibirBlocosProdutos" INTEGER NOT NULL DEFAULT 1,
                        "ExibirCategorias" INTEGER NOT NULL DEFAULT 1,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NOT NULL,
                        "DeletedAt" TEXT NULL,
                        "SyncStatus" INTEGER NOT NULL
                    );
                    """);
                await ExecuteNonQueryAsync(
                    connection,
                    """CREATE INDEX "IX_Configuracoes_SyncStatus" ON "Configuracoes" ("SyncStatus");""");
            }

            await AddColumnIfMissingAsync(connection, "Configuracoes", "ExibirBlocosProdutos", "INTEGER NOT NULL DEFAULT 1");
            await AddColumnIfMissingAsync(connection, "Configuracoes", "ExibirCategorias", "INTEGER NOT NULL DEFAULT 1");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        if (await db.Configuracoes.AnyAsync())
            return;

        db.Add(Configuracao.Criar());
        await db.SaveChangesAsync();
    }

    private static async Task MigrateInscricaoTerminalAsync(PosDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            if (await HasTableAsync(connection, "InscricoesTerminais"))
            {
                await AddColumnIfMissingAsync(connection, "InscricoesTerminais", "CatalogoLidoEm", "TEXT NULL");
                if (!await HasColumnAsync(connection, "InscricoesTerminais", "CatalogoCursorReiniciado"))
                {
                    await ExecuteNonQueryAsync(connection, """UPDATE "InscricoesTerminais" SET "CatalogoLidoEm" = NULL;""");
                    await AddColumnIfMissingAsync(connection, "InscricoesTerminais", "CatalogoCursorReiniciado", "INTEGER NOT NULL DEFAULT 1");
                }

                return;
            }

            await ExecuteNonQueryAsync(
                connection,
                """
                CREATE TABLE "InscricoesTerminais" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_InscricoesTerminais" PRIMARY KEY,
                    "EnderecoApi" TEXT NOT NULL,
                    "LojaId" TEXT NOT NULL,
                    "TerminalId" TEXT NOT NULL,
                    "SegredoProtegido" TEXT NOT NULL,
                    "CatalogoLidoEm" TEXT NULL,
                    "CatalogoCursorReiniciado" INTEGER NOT NULL DEFAULT 1
                );
                """);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task SeedAsync(PosDbContext db, IPasswordHasher hasher)
    {
        if (await db.Users.AnyAsync())
            return;

        var store = Store.Create("LOJA-01", "Açaí Prime Centro");
        var terminal = Terminal.Create(store.Id, "PDV-01", "Terminal Principal");

        var admin = User.Create("Administrador", "admin", hasher.Hash("1234"), UserRole.Administrator);
        var manager = User.Create("Gerente", "gerente", hasher.Hash("1234"), UserRole.Manager);
        var op = User.Create("Operador", "operador", hasher.Hash("1234"), UserRole.Operator);

        var bowls = Category.Create("Bowls", "Açaí em bowls", 1);
        var drinks = Category.Create("Bebidas", "Sucos e refrigerantes", 2);
        var toppings = Category.Create("Adicionais", "Complementos", 3);
        var snacks = Category.Create("Lanches", "Opções salgadas", 4);

        var products = new[]
        {
            Product.Create("Açaí 300ml", "ACAI-300", [bowls.Id], 18.90m, "7891001003001", 7.50m, 100, ncm: "20079990"),
            Product.Create("Açaí 500ml", "ACAI-500", [bowls.Id], 24.90m, "7891001005001", 10.00m, 100, ncm: "20079990"),
            Product.Create("Açaí 700ml", "ACAI-700", [bowls.Id], 29.90m, "7891001007001", 12.50m, 80, ncm: "20079990"),
            Product.Create("Açaí 1L", "ACAI-1000", [bowls.Id], 39.90m, "7891001010001", 16.00m, 50, ncm: "20079990"),
            Product.Create("Água Mineral 500ml", "AGUA-500", [drinks.Id], 4.00m, "7891002005001", 1.20m, 200, ncm: "22011000"),
            Product.Create("Suco Natural 400ml", "SUCO-400", [drinks.Id], 9.90m, "7891002004001", 3.50m, 60, ncm: "20098990"),
            Product.Create("Refrigerante Lata", "REFRI-350", [drinks.Id], 6.50m, "7891002003501", 2.80m, 120, ncm: "22021000"),
            Product.Create("Granola", "ADD-GRANOLA", [toppings.Id], 3.50m, "7891003000001", 1.00m, 200, ncm: "19042000"),
            Product.Create("Banana", "ADD-BANANA", [toppings.Id], 2.50m, "7891003000002", 0.80m, 150, ncm: "08039000"),
            Product.Create("Leite Condensado", "ADD-LEITE", [toppings.Id], 3.00m, "7891003000003", 1.10m, 150, ncm: "04029900"),
            Product.Create("Paçoca", "ADD-PACOCA", [toppings.Id], 2.50m, "7891003000004", 0.90m, 150, ncm: "17049020"),
            Product.Create("Misto Quente", "LANCHE-MISTO", [snacks.Id], 14.90m, "7891004000001", 6.00m, 40, ncm: "19059090"),
        };

        db.Add(store);
        db.Add(terminal);
        db.Add(admin);
        db.Add(manager);
        db.Add(op);
        db.Add(bowls);
        db.Add(drinks);
        db.Add(toppings);
        db.Add(snacks);
        foreach (var product in products)
            db.Add(product);

        await db.SaveChangesAsync();
    }
}
