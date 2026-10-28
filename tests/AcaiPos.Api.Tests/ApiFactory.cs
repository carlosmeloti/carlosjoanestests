using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AcaiPos.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"acaipos-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Central:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Central", $"Data Source={_databasePath}");
        builder.UseSetting("Sync:ChaveAssinatura", new string('k', 48));
        builder.UseSetting("Sync:ChaveAdmin", "chave-admin-teste");
        builder.UseSetting("Sync:TokenMinutos", "15");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !File.Exists(_databasePath))
            return;

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
        }
    }
}
