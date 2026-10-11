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
    private bool _showingUnhandledError;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // OnStartup yields at the first await, before any window exists.
        // OnLastWindowClose would shut the process down in that gap.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
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
            MainWindow = window;
            window.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "AcaiPos", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AcaiPos");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "last-error.txt"), e.Exception.ToString());
        }
        catch (Exception)
        {
            // The dialog below still reports the original failure.
        }

        e.Handled = true;
        if (_showingUnhandledError)
            return;

        _showingUnhandledError = true;
        try
        {
            MessageBox.Show(e.Exception.Message, "AcaiPos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _showingUnhandledError = false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
