using AcaiPos.Application.Tickets;

namespace AcaiPos.Desktop.Printing;

/// <summary>
/// Imprime o movimento de caixa na bobina padrão ou em A4, com diálogo.
/// </summary>
public interface ICashMovementPrinter
{
    /// <summary>
    /// Envia o texto de 42 colunas à impressora padrão, sem diálogo.
    /// </summary>
    Task PrintThermalAsync(CashMovementReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre o diálogo de impressão em A4. Devolve falso se o operador cancelar.
    /// </summary>
    Task<bool> PrintA4Async(CashMovementReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera as páginas A4 em PNG, sem diálogo e sem enviar à impressora.
    /// </summary>
    IReadOnlyList<byte[]> RenderA4PreviewPngs(CashMovementReport report);
}

/// <summary>
/// Encaminha o ciclo formatado para a bobina ou para a página A4.
/// </summary>
public sealed class CashMovementPrinter : ICashMovementPrinter
{
    private readonly SpoolerTextPrinter _thermal;
    private readonly CashMovementTextFormatter _formatter = new();
    private readonly CashMovementA4Printer _a4 = new();

    public CashMovementPrinter(SpoolerTextPrinter thermal)
    {
        _thermal = thermal;
    }

    /// <inheritdoc />
    public Task PrintThermalAsync(CashMovementReport report, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = _formatter.Format(report);
        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("A impressão precisa da interface do PDV.");

        return dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _thermal.Print("Movimento de caixa", text);
        }).Task;
    }

    /// <inheritdoc />
    public Task<bool> PrintA4Async(CashMovementReport report, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("A impressão precisa da interface do PDV.");

        return dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _a4.ShowAndPrint(report);
        }).Task;
    }

    /// <inheritdoc />
    public IReadOnlyList<byte[]> RenderA4PreviewPngs(CashMovementReport report)
        => _a4.RenderPreviewPngs(report);
}
