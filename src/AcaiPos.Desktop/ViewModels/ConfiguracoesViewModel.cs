using System.Windows.Input;
using AcaiPos.Application.Services;
using AcaiPos.Desktop.Behaviors;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Desktop.ViewModels;

public sealed class ConfiguracoesViewModel : ObservableObject
{
    private readonly ConfiguracaoService _service;
    private readonly InscricaoTerminalService _inscricao;
    private readonly Action<string>? _notify;
    private readonly Action<bool, bool>? _aoMudarExibicao;
    private ModoInterface _modo = ModoInterface.Touchscreen;
    private bool _exibirBlocosProdutos = true;
    private bool _exibirCategorias = true;
    private bool _carregando;
    private bool _aplicando;
    private string _enderecoApi = string.Empty;
    private string _lojaId = string.Empty;
    private string _terminalId = string.Empty;
    private string _segredo = string.Empty;
    private bool _temSegredo;

    public ConfiguracoesViewModel(
        ConfiguracaoService service,
        InscricaoTerminalService inscricao,
        Action<string>? notify = null,
        Action<bool, bool>? aoMudarExibicao = null)
    {
        _service = service;
        _inscricao = inscricao;
        _notify = notify;
        _aoMudarExibicao = aoMudarExibicao;
        SalvarInscricaoCommand = new RelayCommand(_ => SalvarInscricaoAsync());
    }

    public string EnderecoApi
    {
        get => _enderecoApi;
        set => SetProperty(ref _enderecoApi, value);
    }

    public string LojaId
    {
        get => _lojaId;
        set => SetProperty(ref _lojaId, value);
    }

    public string TerminalId
    {
        get => _terminalId;
        set => SetProperty(ref _terminalId, value);
    }

    public string Segredo
    {
        get => _segredo;
        set => SetProperty(ref _segredo, value);
    }

    public bool TemSegredo
    {
        get => _temSegredo;
        private set
        {
            if (SetProperty(ref _temSegredo, value))
                RaisePropertyChanged(nameof(AvisoSegredo));
        }
    }

    public string AvisoSegredo => _temSegredo
        ? "Segredo já gravado neste PDV. Deixe o campo vazio para mantê-lo."
        : "Digite o segredo recebido na inscrição. Depois de gravar, o campo fica vazio.";

    public ICommand SalvarInscricaoCommand { get; }

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

    public bool ExibirBlocosProdutos
    {
        get => _exibirBlocosProdutos;
        set
        {
            if (_carregando || value == _exibirBlocosProdutos)
                return;

            _ = DefinirExibicaoAsync(value, _exibirCategorias);
        }
    }

    public bool ExibirCategorias
    {
        get => _exibirCategorias;
        set
        {
            if (_carregando || value == _exibirCategorias)
                return;

            _ = DefinirExibicaoAsync(_exibirBlocosProdutos, value);
        }
    }

    public async Task LoadAsync()
    {
        _carregando = true;
        try
        {
            _modo = await _service.ObterModoInterfaceAsync();
            var exibicao = await _service.ObterExibicaoVendaAsync();
            _exibirBlocosProdutos = exibicao.BlocosProdutos;
            _exibirCategorias = exibicao.Categorias;
            FloatingKeyboards.SetEnabled(_modo == ModoInterface.Touchscreen);
            RaisePropertyChanged(nameof(Touchscreen));
            RaisePropertyChanged(nameof(DispositivosPadrao));
            RaisePropertyChanged(nameof(ExibirBlocosProdutos));
            RaisePropertyChanged(nameof(ExibirCategorias));
            await CarregarInscricaoAsync();
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

    private async Task DefinirExibicaoAsync(bool blocosProdutos, bool categorias)
    {
        if (_carregando || _aplicando || (_exibirBlocosProdutos == blocosProdutos && _exibirCategorias == categorias))
        {
            RaisePropertyChanged(nameof(ExibirBlocosProdutos));
            RaisePropertyChanged(nameof(ExibirCategorias));
            return;
        }

        var anteriorBlocos = _exibirBlocosProdutos;
        var anteriorCategorias = _exibirCategorias;
        _aplicando = true;
        _exibirBlocosProdutos = blocosProdutos;
        _exibirCategorias = categorias;
        RaisePropertyChanged(nameof(ExibirBlocosProdutos));
        RaisePropertyChanged(nameof(ExibirCategorias));

        try
        {
            await _service.DefinirExibicaoVendaAsync(blocosProdutos, categorias);
            _aoMudarExibicao?.Invoke(blocosProdutos, categorias);
        }
        catch (Exception ex)
        {
            _exibirBlocosProdutos = anteriorBlocos;
            _exibirCategorias = anteriorCategorias;
            RaisePropertyChanged(nameof(ExibirBlocosProdutos));
            RaisePropertyChanged(nameof(ExibirCategorias));
            _notify?.Invoke(ex is DomainException ? ex.Message : $"Configuração não salva: {ex.Message}");
        }
        finally
        {
            _aplicando = false;
        }
    }

    private async Task CarregarInscricaoAsync()
    {
        var atual = await _inscricao.ObterAsync();
        _enderecoApi = atual?.EnderecoApi ?? string.Empty;
        _lojaId = atual is null ? string.Empty : atual.LojaId.ToString();
        _terminalId = atual is null ? string.Empty : atual.TerminalId.ToString();
        _segredo = string.Empty;
        _temSegredo = atual?.TemSegredo ?? false;
        RaisePropertyChanged(nameof(EnderecoApi));
        RaisePropertyChanged(nameof(LojaId));
        RaisePropertyChanged(nameof(TerminalId));
        RaisePropertyChanged(nameof(Segredo));
        RaisePropertyChanged(nameof(TemSegredo));
        RaisePropertyChanged(nameof(AvisoSegredo));
    }

    private async Task SalvarInscricaoAsync()
    {
        try
        {
            await _inscricao.SalvarAsync(_enderecoApi, _lojaId, _terminalId, _segredo);
            _segredo = string.Empty;
            _temSegredo = true;
            RaisePropertyChanged(nameof(Segredo));
            RaisePropertyChanged(nameof(TemSegredo));
            RaisePropertyChanged(nameof(AvisoSegredo));
            _notify?.Invoke("Inscrição gravada. O próximo ciclo de sincronização passa a usá-la.");
        }
        catch (Exception ex)
        {
            _notify?.Invoke(ex is DomainException ? ex.Message : $"Inscrição não salva: {ex.Message}");
        }
    }
}
