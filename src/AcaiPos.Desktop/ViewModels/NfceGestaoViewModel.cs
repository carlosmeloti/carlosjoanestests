using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Desktop.Printing;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;
using Microsoft.Win32;

namespace AcaiPos.Desktop.ViewModels;

/// <summary>
/// Opção do filtro de status da NFC-e. Valor nulo significa todos os status.
/// </summary>
public sealed class NfceGestaoStatusOption
{
    public string Label { get; init; } = string.Empty;
    public StatusDocumentoFiscal? Status { get; init; }
}

/// <summary>
/// Lista paginada das NFC-e, com pesquisa, cancelamento, envio, XML e relatório.
/// </summary>
public sealed class NfceGestaoViewModel : ObservableObject
{
    private const int TamanhoPagina = 15;

    private readonly NfceService _nfceService;
    private readonly SaleService _saleService;
    private readonly ICurrentSession _session;
    private readonly NfceRelatorioA4Printer _printer = new();
    private readonly Action<string>? _notify;

    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today;
    private NfceGestaoStatusOption? _selectedStatus;
    private string _ticket = string.Empty;
    private bool _somenteCaixaAberto = true;
    private string _message = "NFC-e do caixa em operação.";
    private string _pageLabel = string.Empty;
    private int _pagina = 1;
    private int _paginas = 1;
    private bool _showCancelPanel;
    private bool _showA4Preview;
    private string _cancelReason = string.Empty;
    private string _authorizerUsername = "gerente";
    private string _authorizerPin = string.Empty;
    private bool _canCancelDirectly;
    private Guid? _cancelSaleId;
    private string _cancelHint = string.Empty;
    private NfceRelatorio? _relatorio;
    private CancelFiscalMode _cancelMode;

    public NfceGestaoViewModel(
        NfceService nfceService,
        SaleService saleService,
        ICurrentSession session,
        Action<string>? notify = null)
    {
        _nfceService = nfceService;
        _saleService = saleService;
        _session = session;
        _notify = notify;

        StatusOptions =
        [
            new NfceGestaoStatusOption { Label = "Todas" },
            new NfceGestaoStatusOption { Label = "Autorizadas", Status = StatusDocumentoFiscal.Autorizada },
            new NfceGestaoStatusOption { Label = "Canceladas", Status = StatusDocumentoFiscal.Cancelada },
            new NfceGestaoStatusOption { Label = "Contingências", Status = StatusDocumentoFiscal.Contingencia }
        ];
        _selectedStatus = StatusOptions[0];

        SearchCommand = new RelayCommand(async _ => await SearchAsync());
        CurrentCashCommand = new RelayCommand(async _ => await ShowCurrentCashAsync());
        PreviousPageCommand = new RelayCommand(async _ => await ChangePageAsync(-1), _ => _pagina > 1);
        NextPageCommand = new RelayCommand(async _ => await ChangePageAsync(1), _ => _pagina < _paginas);
        ExportXmlCommand = new RelayCommand(async p => await ExportXmlAsync(p as NfceGestaoItemDto));
        TransmitCommand = new RelayCommand(async p => await TransmitAsync(p as NfceGestaoItemDto));
        BeginCancelCommand = new RelayCommand(p => BeginCancel(p as NfceGestaoItemDto));
        ConfirmCancelCommand = new RelayCommand(async _ => await ConfirmCancelAsync());
        DismissCancelCommand = new RelayCommand(_ => CloseCancelPanel());
        ReportCommand = new RelayCommand(async _ => await OpenA4PreviewAsync());
        CloseReportCommand = new RelayCommand(_ => ClosePreview());
        ConfirmA4PrintCommand = new RelayCommand(async _ => await ConfirmA4PrintAsync(), _ => _relatorio is not null);
    }

    public ObservableCollection<NfceGestaoItemDto> Items { get; } = [];
    public IReadOnlyList<NfceGestaoStatusOption> StatusOptions { get; }

    public RelayCommand SearchCommand { get; }
    public RelayCommand CurrentCashCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand ExportXmlCommand { get; }
    public RelayCommand TransmitCommand { get; }
    public RelayCommand BeginCancelCommand { get; }
    public RelayCommand ConfirmCancelCommand { get; }
    public RelayCommand DismissCancelCommand { get; }
    public RelayCommand ReportCommand { get; }
    public RelayCommand CloseReportCommand { get; }
    public RelayCommand ConfirmA4PrintCommand { get; }
    public ObservableCollection<BitmapImage> A4PreviewPages { get; } = [];

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public NfceGestaoStatusOption? SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public string Ticket
    {
        get => _ticket;
        set => SetProperty(ref _ticket, value);
    }

    public bool SomenteCaixaAberto
    {
        get => _somenteCaixaAberto;
        set => SetProperty(ref _somenteCaixaAberto, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string PageLabel
    {
        get => _pageLabel;
        set => SetProperty(ref _pageLabel, value);
    }

    public bool ShowCancelPanel
    {
        get => _showCancelPanel;
        set => SetProperty(ref _showCancelPanel, value);
    }

    public bool ShowA4Preview
    {
        get => _showA4Preview;
        private set => SetProperty(ref _showA4Preview, value);
    }

    public string CancelReason
    {
        get => _cancelReason;
        set => SetProperty(ref _cancelReason, value);
    }

    public string AuthorizerUsername
    {
        get => _authorizerUsername;
        set => SetProperty(ref _authorizerUsername, value);
    }

    public string AuthorizerPin
    {
        get => _authorizerPin;
        set => SetProperty(ref _authorizerPin, value);
    }

    public bool CanCancelDirectly
    {
        get => _canCancelDirectly;
        set
        {
            SetProperty(ref _canCancelDirectly, value);
            RaisePropertyChanged(nameof(AuthorizerVisibility));
        }
    }

    public string AuthorizerVisibility => CanCancelDirectly ? "Collapsed" : "Visible";
    public string CancelHint => _cancelHint;

    /// <summary>
    /// Abre a tela na lista do caixa em operação.
    /// </summary>
    public Task InitializeAsync() => ShowCurrentCashAsync();

    private async Task ShowCurrentCashAsync()
    {
        SomenteCaixaAberto = true;
        var abertoEm = _session.OpenCashRegister?.OpenedAt;
        var inicio = abertoEm is null
            ? DateTime.Today
            : (abertoEm.Value.Kind == DateTimeKind.Utc ? abertoEm.Value.ToLocalTime() : abertoEm.Value).Date;
        FromDate = inicio;
        ToDate = DateTime.Today < inicio ? inicio : DateTime.Today;
        SelectedStatus = StatusOptions[0];
        Ticket = string.Empty;
        _pagina = 1;
        await LoadAsync();
    }

    private async Task SearchAsync()
    {
        if (ToDate.Date < FromDate.Date)
        {
            Message = "A data final precisa ser igual ou posterior à inicial.";
            return;
        }

        _pagina = 1;
        await LoadAsync();
    }

    private async Task ChangePageAsync(int delta)
    {
        _pagina += delta;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var pagina = await _nfceService.ListarAsync(MontarConsulta());
            _pagina = pagina.Pagina;
            _paginas = pagina.Paginas;
            Items.Clear();
            foreach (var item in pagina.Itens)
                Items.Add(item);

            PageLabel = pagina.Mensagem;
            Message = SomenteCaixaAberto
                ? "NFC-e do caixa em operação."
                : "Pesquisa de NFC-e.";
            if (pagina.Total == 0)
                Message = pagina.Mensagem;
        }
        catch (Exception ex)
        {
            Items.Clear();
            PageLabel = string.Empty;
            Message = $"Erro ao listar as NFC-e: {ex.Message}";
        }

        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private NfceGestaoConsulta MontarConsulta()
    {
        DateTime? from = DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Local).ToUniversalTime();
        DateTime? to = DateTime.SpecifyKind(ToDate.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        return new NfceGestaoConsulta(
            SomenteCaixaAberto,
            from,
            to,
            SelectedStatus?.Status,
            string.IsNullOrWhiteSpace(Ticket) ? null : Ticket.Trim(),
            _pagina,
            TamanhoPagina);
    }

    private async Task ExportXmlAsync(NfceGestaoItemDto? item)
    {
        if (item is null || !item.PodeExportarXml)
            return;

        try
        {
            var xml = await _nfceService.ObterXmlDocumentoAsync(item.Id);
            if (xml is null)
            {
                Message = "XML da NFC-e não encontrado.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "XML (*.xml)|*.xml",
                FileName = xml.NomeArquivo,
                DefaultExt = ".xml",
                AddExtension = true
            };
            if (dialog.ShowDialog() != true)
                return;

            await File.WriteAllTextAsync(dialog.FileName, xml.Conteudo);
            Message = "XML da NFC-e salvo.";
            _notify?.Invoke(Message);
        }
        catch (Exception ex)
        {
            Message = $"Erro ao salvar o XML: {ex.Message}";
        }
    }

    private async Task TransmitAsync(NfceGestaoItemDto? item)
    {
        if (item?.SaleId is not Guid saleId || !item.PodeEnviar)
            return;

        try
        {
            Message = await _nfceService.TransmitirDocumentoAsync(saleId);
            _notify?.Invoke(Message);
            await LoadAsync();
        }
        catch (DomainException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Erro ao enviar a NFC-e: {ex.Message}";
        }
    }

    private void BeginCancel(NfceGestaoItemDto? item)
    {
        if (item is null || !item.PodeCancelar || item.SaleId is not Guid saleId)
        {
            Message = "Esta NFC-e não pode ser cancelada por aqui.";
            return;
        }

        _cancelSaleId = saleId;
        CancelReason = string.Empty;
        AuthorizerPin = string.Empty;
        _cancelMode = item.CancelaNaSefaz
            ? CancelFiscalMode.Sefaz
            : item.CancelaContingencia
                ? CancelFiscalMode.Contingencia
                : CancelFiscalMode.Local;
        _cancelHint = _cancelMode switch
        {
            CancelFiscalMode.Sefaz => "A NFC-e autorizada será cancelada na SEFAZ. O motivo precisa ter entre 15 e 255 caracteres. Estoque e pagamentos serão revertidos.",
            CancelFiscalMode.Contingencia => "A NFC-e em contingência não será enviada à SEFAZ e a venda será cancelada no PDV. Estoque e pagamentos serão revertidos.",
            _ => "A venda será cancelada no PDV. Estoque e pagamentos serão revertidos."
        };
        RaisePropertyChanged(nameof(CancelHint));
        CanCancelDirectly = _saleService.CurrentUserCanCancelSales;
        ShowCancelPanel = true;
        Message = CanCancelDirectly
            ? $"Informe o motivo para cancelar a venda {item.TicketNumber}."
            : $"Cancelamento da venda {item.TicketNumber} exige autorização de gerente/admin.";
    }

    private async Task ConfirmCancelAsync()
    {
        if (!_cancelSaleId.HasValue)
            return;

        var modo = _cancelMode;
        try
        {
            await _nfceService.CancelarVendaAsync(new CancelSaleRequest(
                _cancelSaleId.Value,
                CancelReason,
                CanCancelDirectly ? null : AuthorizerUsername,
                CanCancelDirectly ? null : AuthorizerPin));

            Message = modo switch
            {
                CancelFiscalMode.Sefaz => "Venda e NFC-e canceladas. Estoque revertido.",
                CancelFiscalMode.Contingencia => "Venda cancelada. A NFC-e em contingência não será enviada. Estoque revertido.",
                _ => "Venda cancelada. Estoque revertido."
            };
            _notify?.Invoke(Message);
            CloseCancelPanel();
            await LoadAsync();
        }
        catch (DomainException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Erro ao cancelar: {ex.Message}";
        }
    }

    private void CloseCancelPanel()
    {
        ShowCancelPanel = false;
        CancelReason = string.Empty;
        AuthorizerPin = string.Empty;
        _cancelSaleId = null;
        _cancelMode = CancelFiscalMode.Local;
        _cancelHint = string.Empty;
        RaisePropertyChanged(nameof(CancelHint));
    }

    private async Task OpenA4PreviewAsync()
    {
        if (ToDate.Date < FromDate.Date)
        {
            Message = "A data final precisa ser igual ou posterior à inicial.";
            return;
        }

        try
        {
            var relatorio = await _nfceService.MontarRelatorioAsync(MontarConsulta());
            var pages = _printer.RenderPreviewPngs(relatorio);
            A4PreviewPages.Clear();
            foreach (var png in pages)
                A4PreviewPages.Add(LoadPng(png));

            _relatorio = relatorio;
            ConfirmA4PrintCommand.RaiseCanExecuteChanged();
            ShowA4Preview = true;
        }
        catch (Exception ex)
        {
            Message = $"Erro ao gerar o relatório: {ex.Message}";
        }
    }

    private Task ConfirmA4PrintAsync()
    {
        if (_relatorio is null)
            return Task.CompletedTask;

        try
        {
            var printed = _printer.ShowAndPrint(_relatorio);
            Message = printed
                ? "Relatório de NFC-e enviado para impressão."
                : "Impressão cancelada.";
            _notify?.Invoke(Message);
        }
        catch (Exception ex)
        {
            Message = $"Falha na impressão: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Fecha a pré-visualização do relatório sem imprimir.
    /// </summary>
    public void ClosePreview()
    {
        ShowA4Preview = false;
        A4PreviewPages.Clear();
        _relatorio = null;
        ConfirmA4PrintCommand.RaiseCanExecuteChanged();
    }

    private static BitmapImage LoadPng(byte[] png)
    {
        var image = new BitmapImage();
        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private enum CancelFiscalMode
    {
        Local,
        Sefaz,
        Contingencia
    }
}
