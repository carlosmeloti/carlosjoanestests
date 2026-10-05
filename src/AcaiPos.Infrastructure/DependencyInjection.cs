using AcaiPos.Application.Abstractions;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;
using AcaiPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AcaiPos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string databasePath)
    {
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
        await SeedAsync(db, hasher);
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
            Product.Create("Açaí 300ml", "ACAI-300", bowls.Id, 18.90m, "7891001003001", 7.50m, 100),
            Product.Create("Açaí 500ml", "ACAI-500", bowls.Id, 24.90m, "7891001005001", 10.00m, 100),
            Product.Create("Açaí 700ml", "ACAI-700", bowls.Id, 29.90m, "7891001007001", 12.50m, 80),
            Product.Create("Açaí 1L", "ACAI-1000", bowls.Id, 39.90m, "7891001010001", 16.00m, 50),
            Product.Create("Água Mineral 500ml", "AGUA-500", drinks.Id, 4.00m, "7891002005001", 1.20m, 200),
            Product.Create("Suco Natural 400ml", "SUCO-400", drinks.Id, 9.90m, "7891002004001", 3.50m, 60),
            Product.Create("Refrigerante Lata", "REFRI-350", drinks.Id, 6.50m, "7891002003501", 2.80m, 120),
            Product.Create("Granola", "ADD-GRANOLA", toppings.Id, 3.50m, "7891003000001", 1.00m, 200),
            Product.Create("Banana", "ADD-BANANA", toppings.Id, 2.50m, "7891003000002", 0.80m, 150),
            Product.Create("Leite Condensado", "ADD-LEITE", toppings.Id, 3.00m, "7891003000003", 1.10m, 150),
            Product.Create("Paçoca", "ADD-PACOCA", toppings.Id, 2.50m, "7891003000004", 0.90m, 150),
            Product.Create("Misto Quente", "LANCHE-MISTO", snacks.Id, 14.90m, "7891004000001", 6.00m, 40),
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
