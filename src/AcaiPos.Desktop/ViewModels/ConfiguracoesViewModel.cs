using AcaiPos.Application.Services;
using AcaiPos.Desktop.Behaviors;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Desktop.ViewModels;

public sealed class ConfiguracoesViewModel : ObservableObject
{
    private readonly ConfiguracaoService _service;
    private readonly Action<string>? _notify;
    private ModoInterface _modo = ModoInterface.Touchscreen;
    private bool _carregando;
    private bool _aplicando;

    public ConfiguracoesViewModel(ConfiguracaoService service, Action<string>? notify = null)
    {
        _service = service;
        _notify = notify;
    }

    public bool Touchscreen
    {
        get => _modo == ModoInterface.Touchscreen;
        set
        {
            if (value)
                _ = AplicarAsync(ModoInterface.Touchscreen);
        }
    }

    public bool DispositivosPadrao
    {
        get => _modo == ModoInterface.DispositivosPadrao;
        set
        {
            if (value)
                _ = AplicarAsync(ModoInterface.DispositivosPadrao);
        }
    }

    public async Task LoadAsync()
    {
        _carregando = true;
        try
        {
            _modo = await _service.ObterModoInterfaceAsync();
            FloatingKeyboards.SetEnabled(_modo == ModoInterface.Touchscreen);
            RaisePropertyChanged(nameof(Touchscreen));
            RaisePropertyChanged(nameof(DispositivosPadrao));
        }
        catch (Exception ex)
        {
            _notify?.Invoke(ex.Message);
        }
        finally
        {
            _carregando = false;
        }
    }

    private async Task AplicarAsync(ModoInterface modo)
    {
        if (_carregando || _aplicando || _modo == modo)
            return;

        var anterior = _modo;
        _aplicando = true;
        _modo = modo;
        FloatingKeyboards.SetEnabled(modo == ModoInterface.Touchscreen);
        RaisePropertyChanged(nameof(Touchscreen));
        RaisePropertyChanged(nameof(DispositivosPadrao));

        try
        {
            await _service.DefinirModoInterfaceAsync(modo);
        }
        catch (Exception ex)
        {
            _modo = anterior;
            FloatingKeyboards.SetEnabled(anterior == ModoInterface.Touchscreen);
            RaisePropertyChanged(nameof(Touchscreen));
            RaisePropertyChanged(nameof(DispositivosPadrao));
            _notify?.Invoke(ex is DomainException ? ex.Message : $"Configuração não salva: {ex.Message}");
        }
        finally
        {
            _aplicando = false;
        }
    }
}
