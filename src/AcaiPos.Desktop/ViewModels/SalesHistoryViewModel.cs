using System.Collections.ObjectModel;
using System.IO;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;
using Microsoft.Win32;

namespace AcaiPos.Desktop.ViewModels;

/// <summary>
/// Opção do filtro de status da venda. Valor nulo significa todos os status.
/// </summary>
public sealed class StatusFilterOption
{
    public string Label { get; init; } = string.Empty;
    public SaleStatus? Value { get; init; }
}

/// <summary>
/// Opção do filtro de forma de pagamento. Valor nulo significa todas as formas.
/// </summary>
public sealed class PaymentFilterOption
{
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>
/// Opção do filtro de NFC-e. Sem status e sem a marca de ausência significa todas.
/// </summary>
public sealed class NfceFilterOption
{
    public string Label { get; init; } = string.Empty;
    public StatusDocumentoFiscal? Status { get; init; }
    public bool SemNfce { get; init; }
}

/// <summary>
/// Histórico de vendas: filtros, totais, detalhe, reimpressão e cancelamento.
/// </summary>
public sealed class SalesHistoryViewModel : ObservableObject
{
    private readonly SaleService _saleService;
    private readonly NfceService _nfceService;
    private readonly CashRegisterService _cashRegisterService;
    private readonly Action<string>? _notify;
    private readonly Func<Guid, Task>? _onViewTicket;
    private readonly Func<Guid, Task>? _onReprintTicket;
    private bool _lastSearchWasCycle;
    private bool _suppressSessionSelection;
    private int _searchVersion;

    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today;
    private Guid? _operatorId = Guid.Empty;
    private Guid? _terminalId = Guid.Empty;
    private StatusFilterOption? _selectedStatus;
    private NfceFilterOption? _selectedNfce;
    private PaymentFilterOption? _selectedPayment;
    private string _searchText = string.Empty;
    private string _message = "Selecione o período e filtre as vendas.";
    private string _salesCountLabel = "0";
    private string _completedCountLabel = "0";
    private string _cancelledCountLabel = "0";
    private string _completedTotalLabel = 0m.ToString("C");
    private string _averageTicketLabel = 0m.ToString("C");
    private SaleHistoryItemDto? _selectedSale;
    private string _detailText = string.Empty;
    private string _dayReportText = string.Empty;
    private bool _showCancelPanel;
    private string _cancelReason = string.Empty;
    private string _authorizerUsername = "gerente";
    private string _authorizerPin = string.Empty;
    private bool _canCancelDirectly;
    private Guid? _cancelSaleId;
    private CancelFiscalMode _cancelMode;
    private CashSessionListItemDto? _selectedSession;

    /// <summary>
    /// Monta os filtros padrão e os comandos de busca, relatório, ticket e cancelamento.
    /// </summary>
    public SalesHistoryViewModel(
        SaleService saleService,
        NfceService nfceService,
        CashRegisterService cashRegisterService,
        Action<string>? notify = null,
        Func<Guid, Task>? onViewTicket = null,
        Func<Guid, Task>? onReprintTicket = null)
    {
        _saleService = saleService;
        _nfceService = nfceService;
        _cashRegisterService = cashRegisterService;
        _notify = notify;
        _onViewTicket = onViewTicket;
        _onReprintTicket = onReprintTicket;

        StatusOptions =
        [
            new StatusFilterOption { Label = "Todos", Value = null },
            new StatusFilterOption { Label = "Concluídas", Value = SaleStatus.Completed },
            new StatusFilterOption { Label = "Canceladas", Value = SaleStatus.Cancelled }
        ];
        _selectedStatus = StatusOptions[0];

        NfceOptions =
        [
            new NfceFilterOption { Label = "Todas" },
            new NfceFilterOption { Label = "Autorizada", Status = StatusDocumentoFiscal.Autorizada },
            new NfceFilterOption { Label = "Contingência", Status = StatusDocumentoFiscal.Contingencia },
            new NfceFilterOption { Label = "Cancelada", Status = StatusDocumentoFiscal.Cancelada },
            new NfceFilterOption { Label = "Sem NFC-e", SemNfce = true }
        ];
        _selectedNfce = NfceOptions[0];

        PaymentOptions.Add(new PaymentFilterOption { Label = "Todas" });
        _selectedPayment = PaymentOptions[0];

        SearchCommand = new RelayCommand(async _ => await SearchAsync());
        TodayCommand = new RelayCommand(async _ => await LoadTodayAsync());
        Last7DaysCommand = new RelayCommand(async _ => await LoadLastDaysAsync(7));
        LoadDayReportCommand = new RelayCommand(async _ => await LoadDayReportAsync());
        ViewDetailsCommand = new RelayCommand(async p => await ViewDetailsAsync(p as SaleHistoryItemDto));
        ReprintCommand = new RelayCommand(async p => await ReprintAsync(p as SaleHistoryItemDto));
        ExportXmlCommand = new RelayCommand(async p => await ExportXmlAsync(p as SaleHistoryItemDto));
        TransmitCommand = new RelayCommand(async p => await TransmitAsync(p as SaleHistoryItemDto));
        BeginCancelCommand = new RelayCommand(p => BeginCancel(p as SaleHistoryItemDto));
        ConfirmCancelCommand = new RelayCommand(async _ => await ConfirmCancelAsync());
        DismissCancelCommand = new RelayCommand(_ => CloseCancelPanel());
        ClearSelectionCommand = new RelayCommand(_ =>
        {
            SelectedSale = null;
            DetailText = string.Empty;
        });
    }

    public ObservableCollection<SaleHistoryItemDto> Sales { get; } = [];
    public ObservableCollection<CashSessionListItemDto> Sessions { get; } = [];
    public ObservableCollection<OperatorOptionDto> Operators { get; } = [];
    public ObservableCollection<TerminalOptionDto> Terminals { get; } = [];
    public IReadOnlyList<StatusFilterOption> StatusOptions { get; }
    public IReadOnlyList<NfceFilterOption> NfceOptions { get; }
    public ObservableCollection<PaymentFilterOption> PaymentOptions { get; } = [];

    public RelayCommand SearchCommand { get; }
    public RelayCommand TodayCommand { get; }
    public RelayCommand Last7DaysCommand { get; }
    public RelayCommand LoadDayReportCommand { get; }
    public RelayCommand ViewDetailsCommand { get; }
    public RelayCommand ReprintCommand { get; }
    public RelayCommand ExportXmlCommand { get; }
    public RelayCommand TransmitCommand { get; }
    public RelayCommand BeginCancelCommand { get; }
    public RelayCommand ConfirmCancelCommand { get; }
    public RelayCommand DismissCancelCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value.Date);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value.Date);
    }

    public Guid? OperatorId
    {
        get => _operatorId;
        set => SetProperty(ref _operatorId, value);
    }

    public Guid? TerminalId
    {
        get => _terminalId;
        set => SetProperty(ref _terminalId, value);
    }

    /// <summary>
    /// Ciclo da busca própria. Trocar o item consulta na hora.
    /// </summary>
    public CashSessionListItemDto? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (_suppressSessionSelection)
                return;

            if (EqualityComparer<CashSessionListItemDto?>.Default.Equals(_selectedSession, value))
                return;

            _selectedSession = value;
            RaisePropertyChanged();
            _ = SearchByCycleAsync();
        }
    }

    public string EmptySessionsText => Sessions.Count == 0
        ? "Nenhum ciclo de caixa neste terminal."
        : string.Empty;

    public StatusFilterOption? SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public NfceFilterOption? SelectedNfce
    {
        get => _selectedNfce;
        set => SetProperty(ref _selectedNfce, value);
    }

    public PaymentFilterOption? SelectedPayment
    {
        get => _selectedPayment;
        set => SetProperty(ref _selectedPayment, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string SalesCountLabel
    {
        get => _salesCountLabel;
        private set => SetProperty(ref _salesCountLabel, value);
    }

    public string CompletedCountLabel
    {
        get => _completedCountLabel;
        private set => SetProperty(ref _completedCountLabel, value);
    }

    public string CancelledCountLabel
    {
        get => _cancelledCountLabel;
        private set => SetProperty(ref _cancelledCountLabel, value);
    }

    public string CompletedTotalLabel
    {
        get => _completedTotalLabel;
        private set => SetProperty(ref _completedTotalLabel, value);
    }

    public string AverageTicketLabel
    {
        get => _averageTicketLabel;
        private set => SetProperty(ref _averageTicketLabel, value);
    }

    public SaleHistoryItemDto? SelectedSale
    {
        get => _selectedSale;
        set
        {
            SetProperty(ref _selectedSale, value);
            RaisePropertyChanged(nameof(HasSelection));
            RaisePropertyChanged(nameof(DetailVisibility));
            RaisePropertyChanged(nameof(CanCancelSelected));
            RaisePropertyChanged(nameof(CancelButtonVisibility));
            RaisePropertyChanged(nameof(ExportXmlVisibility));
            RaisePropertyChanged(nameof(TransmitVisibility));
            if (value is not null)
                DetailText = BuildDetail(value);
        }
    }

    public bool HasSelection => SelectedSale is not null;
    public string DetailVisibility => HasSelection ? "Visible" : "Collapsed";
    public bool CanCancelSelected => SelectedSale is { Status: SaleStatus.Completed };
    public string CancelButtonVisibility => CanCancelSelected ? "Visible" : "Collapsed";
    public string ExportXmlVisibility => SelectedSale?.PodeExportarXml == true ? "Visible" : "Collapsed";
    public string TransmitVisibility => SelectedSale?.PodeEnviarNfce == true ? "Visible" : "Collapsed";
    public string CancelHint => _cancelMode switch
    {
        CancelFiscalMode.Sefaz => "A NFC-e autorizada também será cancelada na SEFAZ. O motivo precisa ter entre 15 e 255 caracteres. Estoque e pagamentos serão revertidos.",
        CancelFiscalMode.Contingencia => "A NFC-e em contingência não será enviada à SEFAZ e a venda será cancelada no PDV. Estoque e pagamentos serão revertidos.",
        _ => "A venda permanece no histórico. Estoque e pagamentos serão revertidos."
    };

    public string DetailText
    {
        get => _detailText;
        set => SetProperty(ref _detailText, value);
    }

    public string DayReportText
    {
        get => _dayReportText;
        set => SetProperty(ref _dayReportText, value);
    }

    public bool ShowCancelPanel
    {
        get => _showCancelPanel;
        set => SetProperty(ref _showCancelPanel, value);
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

    /// <summary>
    /// Carrega operadores, terminais e ciclos, e busca as vendas do ciclo atual.
    /// </summary>
    public async Task InitializeAsync()
    {
        CanCancelDirectly = _saleService.CurrentUserCanCancelSales;

        Operators.Clear();
        Operators.Add(new OperatorOptionDto(Guid.Empty, "Todos operadores"));
        foreach (var op in await _saleService.GetOperatorOptionsAsync())
            Operators.Add(op);

        Terminals.Clear();
        Terminals.Add(new TerminalOptionDto(Guid.Empty, "Todos", "Todos terminais"));
        foreach (var terminal in await _saleService.GetTerminalOptionsAsync())
            Terminals.Add(terminal);

        OperatorId = Guid.Empty;
        TerminalId = Guid.Empty;

        var currentDescription = SelectedPayment?.Description;
        PaymentOptions.Clear();
        PaymentOptions.Add(new PaymentFilterOption { Label = "Todas" });
        foreach (var description in await _saleService.ListPaymentDescriptionsAsync())
            PaymentOptions.Add(new PaymentFilterOption { Label = description, Description = description });

        SelectedPayment = PaymentOptions.FirstOrDefault(option => option.Description == currentDescription)
            ?? PaymentOptions[0];

        await LoadSessionsAsync();
        await SearchByCycleAsync();
    }

    /// <summary>
    /// Lista os ciclos do terminal e pré-seleciona o aberto, ou o mais recente.
    /// </summary>
    private async Task LoadSessionsAsync()
    {
        var sessions = await _cashRegisterService.ListSessionsAsync();
        var next = sessions.FirstOrDefault(session => session.IsOpen) ?? sessions.FirstOrDefault();

        _suppressSessionSelection = true;
        Sessions.Clear();
        foreach (var session in sessions)
            Sessions.Add(session);

        _selectedSession = next;
        RaisePropertyChanged(nameof(SelectedSession));
        _suppressSessionSelection = false;
        RaisePropertyChanged(nameof(EmptySessionsText));
    }

    /// <summary>
    /// Restringe o período ao dia atual e executa a busca.
    /// </summary>
    private async Task LoadTodayAsync()
    {
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;
        await SearchAsync();
    }

    /// <summary>
    /// Ajusta o período para os últimos dias informados, incluindo hoje, e executa a busca.
    /// </summary>
    private async Task LoadLastDaysAsync(int days)
    {
        ToDate = DateTime.Today;
        FromDate = DateTime.Today.AddDays(-(days - 1));
        await SearchAsync();
    }

    /// <summary>
    /// Consulta as vendas do filtro de período, atualiza totais e o resumo do dia final.
    /// </summary>
    private async Task SearchAsync()
    {
        var version = ++_searchVersion;

        try
        {
            if (ToDate < FromDate)
                throw new DomainException("A data final não pode ser menor que a inicial.");

            _lastSearchWasCycle = false;

            var fromUtc = DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Local).ToUniversalTime();
            var toUtcExclusive = DateTime.SpecifyKind(ToDate.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var filter = new SaleHistoryFilter(
                fromUtc,
                toUtcExclusive,
                OperatorId == Guid.Empty ? null : OperatorId,
                TerminalId == Guid.Empty ? null : TerminalId,
                SelectedStatus?.Value,
                SelectedPayment?.Description,
                SearchText,
                NfceStatus: SelectedNfce?.Status,
                SemNfce: SelectedNfce?.SemNfce == true);

            var items = await _saleService.SearchHistoryAsync(filter);
            if (version != _searchVersion)
                return;

            var message = items.Count == 0
                ? "Nenhuma venda encontrada para o filtro."
                : $"Exibindo {items.Count} venda(s).";

            await ApplyResultsAsync(items, message);
            if (version != _searchVersion)
                return;
            await LoadDayReportAsync();
            _notify?.Invoke(Message);
        }
        catch (DomainException ex)
        {
            if (version == _searchVersion)
                Message = ex.Message;
        }
        catch (Exception ex)
        {
            if (version == _searchVersion)
                Message = $"Erro ao consultar histórico: {ex.Message}";
        }
    }

    /// <summary>
    /// Consulta só as vendas do ciclo selecionado, sem os filtros de período.
    /// </summary>
    private async Task SearchByCycleAsync()
    {
        var version = ++_searchVersion;

        try
        {
            _lastSearchWasCycle = true;
            var session = SelectedSession;

            if (session is null)
            {
                await ApplyResultsAsync([], "Nenhum ciclo de caixa neste terminal.");
                if (version != _searchVersion)
                    return;

                _notify?.Invoke(Message);
                return;
            }

            var items = await _saleService.SearchHistoryAsync(
                new SaleHistoryFilter(
                    CashRegisterId: session.Id,
                    NfceStatus: SelectedNfce?.Status,
                    SemNfce: SelectedNfce?.SemNfce == true));
            if (version != _searchVersion)
                return;

            var message = items.Count == 0
                ? "Nenhuma venda encontrada neste ciclo."
                : $"Exibindo {items.Count} venda(s) do ciclo.";

            await ApplyResultsAsync(items, message);
            if (version != _searchVersion)
                return;

            _notify?.Invoke(Message);
        }
        catch (DomainException ex)
        {
            if (version == _searchVersion)
                Message = ex.Message;
        }
        catch (Exception ex)
        {
            if (version == _searchVersion)
                Message = $"Erro ao consultar histórico: {ex.Message}";
        }
    }

    /// <summary>
    /// Preenche a lista, os totais e a mensagem da última consulta.
    /// </summary>
    private async Task ApplyResultsAsync(IReadOnlyList<SaleHistoryItemDto> items, string message)
    {
        Sales.Clear();
        foreach (var item in items)
            Sales.Add(item);

        var totals = await _saleService.GetHistoryTotalsAsync(items);
        SalesCountLabel = totals.Count.ToString();
        CompletedCountLabel = totals.CompletedCount.ToString();
        CancelledCountLabel = totals.CancelledCount.ToString();
        CompletedTotalLabel = totals.CompletedTotal.ToString("C");
        AverageTicketLabel = totals.AverageTicket.ToString("C");
        Message = message;
        SelectedSale = null;
    }

    /// <summary>
    /// Monta o texto do resumo do dia da data final, por pagamento e por operador.
    /// </summary>
    private async Task LoadDayReportAsync()
    {
        try
        {
            var report = await _saleService.GetDayReportAsync(ToDate);
            var payments = report.ByPaymentMethod.Count == 0
                ? "sem pagamentos"
                : string.Join(" · ", report.ByPaymentMethod.Select(p => $"{p.MethodLabel} {p.Amount:C}"));
            var operators = report.ByOperator.Count == 0
                ? "sem operadores"
                : string.Join(" · ", report.ByOperator.Select(o => $"{o.OperatorName} ({o.SaleCount}) {o.Total:C}"));

            DayReportText =
                $"Resumo {report.Day:dd/MM}: {report.SaleCount} venda(s), {report.CancelledCount} cancelada(s), " +
                $"bruto {report.GrossTotal:C}, descontos {report.DiscountTotal:C}, acréscimos {report.SurchargeTotal:C}, líquido {report.NetTotal:C}. " +
                $"Pagamentos: {payments}. Por operador: {operators}.";
        }
        catch (Exception ex)
        {
            DayReportText = $"Falha ao montar resumo: {ex.Message}";
        }
    }

    /// <summary>
    /// Seleciona a venda e abre o ticket correspondente.
    /// </summary>
    private async Task ViewDetailsAsync(SaleHistoryItemDto? item)
    {
        SelectedSale = item ?? SelectedSale;
        if (SelectedSale is null || _onViewTicket is null)
            return;

        await _onViewTicket(SelectedSale.Id);
    }

    /// <summary>
    /// Pede a reimpressão do ticket da venda informada ou da selecionada.
    /// </summary>
    private async Task ReprintAsync(SaleHistoryItemDto? item)
    {
        var sale = item ?? SelectedSale;
        if (sale is null || _onReprintTicket is null)
            return;

        await _onReprintTicket(sale.Id);
    }

    private async Task ExportXmlAsync(SaleHistoryItemDto? item)
    {
        var sale = item ?? SelectedSale;
        if (sale is null || !sale.PodeExportarXml)
            return;

        try
        {
            var xml = await _nfceService.ObterXmlAsync(sale.Id);
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

    private async Task TransmitAsync(SaleHistoryItemDto? item)
    {
        var sale = item ?? SelectedSale;
        if (sale is null || !sale.PodeEnviarNfce)
            return;

        try
        {
            Message = await _nfceService.TransmitirDocumentoAsync(sale.Id);
            _notify?.Invoke(Message);
            if (_lastSearchWasCycle)
                await SearchByCycleAsync();
            else
                await SearchAsync();
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

    /// <summary>
    /// Abre o painel de cancelamento da venda concluída, pedindo autorização quando o operador não pode cancelar sozinho.
    /// </summary>
    private void BeginCancel(SaleHistoryItemDto? item)
    {
        var sale = item ?? SelectedSale;
        if (sale is null || sale.Status != SaleStatus.Completed)
            return;

        SelectedSale = sale;
        _cancelSaleId = sale.Id;
        CancelReason = string.Empty;
        AuthorizerPin = string.Empty;
        _cancelMode = sale.CancelaNaSefaz
            ? CancelFiscalMode.Sefaz
            : sale.CancelaContingencia
                ? CancelFiscalMode.Contingencia
                : CancelFiscalMode.Local;
        RaisePropertyChanged(nameof(CancelHint));
        CanCancelDirectly = _saleService.CurrentUserCanCancelSales;
        ShowCancelPanel = true;
        Message = CanCancelDirectly
            ? $"Informe o motivo para cancelar {sale.TicketNumber}."
            : $"Cancelamento de {sale.TicketNumber} exige autorização de gerente/admin.";
    }

    /// <summary>
    /// Cancela a venda, atualiza a lista e reabre o ticket já cancelado.
    /// </summary>
    private async Task ConfirmCancelAsync()
    {
        if (!_cancelSaleId.HasValue)
            return;

        var saleId = _cancelSaleId.Value;

        var modo = _cancelMode;
        try
        {
            await _nfceService.CancelarVendaAsync(new CancelSaleRequest(
                saleId,
                CancelReason,
                CanCancelDirectly ? null : AuthorizerUsername,
                CanCancelDirectly ? null : AuthorizerPin));

            Message = modo switch
            {
                CancelFiscalMode.Sefaz => "Venda e NFC-e canceladas. Estoque revertido.",
                CancelFiscalMode.Contingencia => "Venda cancelada. A NFC-e em contingência não será enviada. Estoque revertido.",
                _ => "Venda cancelada com sucesso. Estoque revertido."
            };
            _notify?.Invoke(Message);
            CloseCancelPanel();
            if (_lastSearchWasCycle)
                await SearchByCycleAsync();
            else
                await SearchAsync();

            if (_onViewTicket is not null)
                await _onViewTicket(saleId);
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

    /// <summary>
    /// Fecha o painel de cancelamento e descarta motivo, PIN e venda pendente.
    /// </summary>
    private void CloseCancelPanel()
    {
        ShowCancelPanel = false;
        CancelReason = string.Empty;
        AuthorizerPin = string.Empty;
        _cancelSaleId = null;
        _cancelMode = CancelFiscalMode.Local;
        RaisePropertyChanged(nameof(CancelHint));
    }

    /// <summary>
    /// Monta o texto do detalhe da venda, incluindo os dados de cancelamento quando houver.
    /// </summary>
    private static string BuildDetail(SaleHistoryItemDto sale)
    {
        var local = sale.SoldAt.Kind == DateTimeKind.Utc ? sale.SoldAt.ToLocalTime() : sale.SoldAt;
        var text =
            $"{sale.TicketNumber}\n" +
            $"{local:dd/MM/yyyy HH:mm:ss}\n" +
            $"Operador: {sale.OperatorName}\n" +
            $"Terminal: {sale.TerminalCode}\n" +
            $"Status: {sale.StatusLabel}\n" +
            $"Itens: {sale.ItemCount}\n" +
            $"Pagamentos: {sale.PaymentSummary}\n" +
            $"Subtotal: {sale.Subtotal:C}\n" +
            $"Desconto: {sale.DiscountAmount:C}\n" +
            $"Acréscimo: {sale.SurchargeAmount:C}\n" +
            $"Total: {sale.Total:C}\n" +
            $"Troco: {sale.ChangeAmount:C}";

        if (sale.Status == SaleStatus.Cancelled)
        {
            var cancelledAt = sale.CancelledAt.HasValue
                ? (sale.CancelledAt.Value.Kind == DateTimeKind.Utc
                    ? sale.CancelledAt.Value.ToLocalTime()
                    : sale.CancelledAt.Value)
                : (DateTime?)null;

            text +=
                $"\n\nCancelada por: {sale.CancelledByName ?? "—"}\n" +
                $"Em: {(cancelledAt?.ToString("dd/MM/yyyy HH:mm:ss") ?? "—")}\n" +
                $"Motivo: {sale.CancellationReason ?? "—"}";
        }

        if (sale.TemNfce)
        {
            text +=
                $"\n\nNFC-e: {sale.NfceStatusLabel}\n" +
                $"Série/Número: {sale.NfceSerie}/{sale.NfceNumero}\n" +
                $"Chave: {sale.NfceChave ?? "—"}\n" +
                $"Protocolo: {sale.NfceProtocolo ?? "—"}\n" +
                $"cStat: {sale.NfceCStat ?? "—"}\n" +
                $"Motivo: {sale.NfceMotivo ?? "—"}";
        }

        return text;
    }

    private enum CancelFiscalMode
    {
        Local,
        Sefaz,
        Contingencia
    }
}
