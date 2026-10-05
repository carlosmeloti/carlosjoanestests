using System.IO;
using System.Windows;
using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Services;
using AcaiPos.Desktop.Printing;
using AcaiPos.Desktop.ViewModels;
using AcaiPos.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AcaiPos.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AcaiPos");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "acai-pos.db");

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddApplication();
                services.AddInfrastructure(dbPath);
                services.AddSingleton<ITicketPrinter, WpfTicketPrinter>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.Services.InitializeDatabaseAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
