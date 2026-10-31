using AcaiPos.Infrastructure.Sync;
using Microsoft.Extensions.Hosting;

namespace AcaiPos.Desktop.Sync;

public sealed class CicloSincronizacao : BackgroundService
{
    private readonly SincronizadorFila _sincronizador;

    public CicloSincronizacao(SincronizadorFila sincronizador) => _sincronizador = sincronizador;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Yield();
                await _sincronizador.ExecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
