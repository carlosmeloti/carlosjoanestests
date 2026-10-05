using System.Collections.ObjectModel;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Desktop.ViewModels;

public sealed class StatusFilterOption
{
    public string Label { get; init; } = string.Empty;
    public SaleStatus? Value { get; init; }
}

public sealed class PaymentFilterOption
{
    public string Label { get; init; } = string.Empty;
    public PaymentMethod? Value { get; init; }
}

public sealed class SalesHistoryViewModel : ObservableObject
{
    private readonly SaleService _saleService;
    private readonly Action<string>? _notify;
    private readonly Func<Guid, Task>? _onViewTicket;
    private readonly Func<Guid, Task>? _onReprintTicket;

    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today;
    private Guid? _operatorId = Guid.Empty;
    private Guid? _terminalId = Guid.Empty;
    private StatusFilterOption? _selectedStatus;
    private PaymentFilterOption? _selectedPayment;
    private string _searchText = string.Empty;
    private string _message = "Selecione o período e filtre as vendas.";
    private string _totalsText = string.Empty;
    private SaleHistoryItemDto? _selectedSale;
    private string _detailText = string.Empty;
    private string _dayReportText = string.Empty;
    private bool _showCancelPanel;
    private string _cancelReason = string.Empty;
    private string _authorizerUsername = "gerente";
    private string _authorizerPin = string.Empty;
    private bool _canCancelDirectly;
    private Guid? _cancelSaleId;

    public SalesHistoryViewModel(
        SaleService saleService,
        Action<string>? notify = null,
        Func<Guid, Task>? onViewTicket = null,
        Func<Guid, Task>? onReprintTicket = null)
    {
        _saleService = saleService;
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

        PaymentOptions =
        [
            new PaymentFilterOption { Label = "Todas", Value = null },
            new PaymentFilterOption { Label = "Dinheiro", Value = PaymentMethod.Cash },
            new PaymentFilterOption { Label = "PIX", Value = PaymentMethod.Pix },
            new PaymentFilterOption { Label = "Débito", Value = PaymentMethod.DebitCard },
            new PaymentFilterOption { Label = "Crédito", Value = PaymentMethod.CreditCard }
        ];
        _selectedPayment = PaymentOptions[0];

        SearchCommand = new RelayCommand(async _ => await SearchAsync());
        TodayCommand = new RelayCommand(async _ => await LoadTodayAsync());
        Last7DaysCommand = new RelayCommand(async _ => await LoadLastDaysAsync(7));
        LoadDayReportCommand = new RelayCommand(async _ => await LoadDayReportAsync());
        ViewDetailsCommand = new RelayCommand(async p => await ViewDetailsAsync(p as SaleHistoryItemDto));
        ReprintCommand = new RelayCommand(async p => await ReprintAsync(p as SaleHistoryItemDto));
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
    public ObservableCollection<OperatorOptionDto> Operators { get; } = [];
    public ObservableCollection<TerminalOptionDto> Terminals { get; } = [];
    public IReadOnlyList<StatusFilterOption> StatusOptions { get; }
    public IReadOnlyList<PaymentFilterOption> PaymentOptions { get; }

    public RelayCommand SearchCommand { get; }
    public RelayCommand TodayCommand { get; }
    public RelayCommand Last7DaysCommand { get; }
    public RelayCommand LoadDayReportCommand { get; }
    public RelayCommand ViewDetailsCommand { get; }
    public RelayCommand ReprintCommand { get; }
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

    public StatusFilterOption? SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
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

    public string TotalsText
    {
        get => _totalsText;
        set => SetProperty(ref _totalsText, value);
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
            if (value is not null)
                DetailText = BuildDetail(value);
        }
    }

    public bool HasSelection => SelectedSale is not null;
    public string DetailVisibility => HasSelection ? "Visible" : "Collapsed";
    public bool CanCancelSelected => SelectedSale is { Status: SaleStatus.Completed };
    public string CancelButtonVisibility => CanCancelSelected ? "Visible" : "Collapsed";

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
        await LoadTodayAsync();
    }

    private async Task LoadTodayAsync()
    {
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;
        await SearchAsync();
    }

    private async Task LoadLastDaysAsync(int days)
    {
        ToDate = DateTime.Today;
        FromDate = DateTime.Today.AddDays(-(days - 1));
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        try
        {
            if (ToDate < FromDate)
                throw new DomainException("A data final não pode ser menor que a inicial.");

            var fromUtc = DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Local).ToUniversalTime();
            var toUtcExclusive = DateTime.SpecifyKind(ToDate.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var filter = new SaleHistoryFilter(
                fromUtc,
                toUtcExclusive,
                OperatorId == Guid.Empty ? null : OperatorId,
                TerminalId == Guid.Empty ? null : TerminalId,
                SelectedStatus?.Value,
                SelectedPayment?.Value,
                SearchText);

            var items = await _saleService.SearchHistoryAsync(filter);
            Sales.Clear();
            foreach (var item in items)
                Sales.Add(item);

            var totals = await _saleService.GetHistoryTotalsAsync(items);
            TotalsText =
                $"{totals.Count} venda(s) · {totals.CompletedCount} concluída(s) · " +
                $"{totals.CancelledCount} cancelada(s) · Total {totals.CompletedTotal:C} · " +
                $"Ticket médio {totals.AverageTicket:C}";

            Message = items.Count == 0
                ? "Nenhuma venda encontrada para o filtro."
                : $"Exibindo {items.Count} venda(s).";

            SelectedSale = null;
            await LoadDayReportAsync();
            _notify?.Invoke(Message);
        }
        catch (DomainException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Erro ao consultar histórico: {ex.Message}";
        }
    }

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
                $"bruto {report.GrossTotal:C}, descontos {report.DiscountTotal:C}, líquido {report.NetTotal:C}. " +
                $"Pagamentos: {payments}. Por operador: {operators}.";
        }
        catch (Exception ex)
        {
            DayReportText = $"Falha ao montar resumo: {ex.Message}";
        }
    }

    private async Task ViewDetailsAsync(SaleHistoryItemDto? item)
    {
        SelectedSale = item ?? SelectedSale;
        if (SelectedSale is null || _onViewTicket is null)
            return;

        await _onViewTicket(SelectedSale.Id);
    }

    private async Task ReprintAsync(SaleHistoryItemDto? item)
    {
        var sale = item ?? SelectedSale;
        if (sale is null || _onReprintTicket is null)
            return;

        await _onReprintTicket(sale.Id);
    }

    private void BeginCancel(SaleHistoryItemDto? item)
    {
        var sale = item ?? SelectedSale;
        if (sale is null || sale.Status != SaleStatus.Completed)
            return;

        SelectedSale = sale;
        _cancelSaleId = sale.Id;
        CancelReason = string.Empty;
        AuthorizerPin = string.Empty;
        CanCancelDirectly = _saleService.CurrentUserCanCancelSales;
        ShowCancelPanel = true;
        Message = CanCancelDirectly
            ? $"Informe o motivo para cancelar {sale.TicketNumber}."
            : $"Cancelamento de {sale.TicketNumber} exige autorização de gerente/admin.";
    }

    private async Task ConfirmCancelAsync()
    {
        if (!_cancelSaleId.HasValue)
            return;

        var saleId = _cancelSaleId.Value;

        try
        {
            await _saleService.CancelSaleAsync(new CancelSaleRequest(
                saleId,
                CancelReason,
                CanCancelDirectly ? null : AuthorizerUsername,
                CanCancelDirectly ? null : AuthorizerPin));

            Message = "Venda cancelada com sucesso. Estoque revertido.";
            _notify?.Invoke(Message);
            CloseCancelPanel();
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

    private void CloseCancelPanel()
    {
        ShowCancelPanel = false;
        CancelReason = string.Empty;
        AuthorizerPin = string.Empty;
        _cancelSaleId = null;
    }

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

        return text;
    }
}
