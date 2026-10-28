using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Application.Tickets;
using AcaiPos.Desktop.Printing;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Desktop.ViewModels;

/// <summary>
/// Linha de sangria ou suprimento exibida no detalhe do ciclo de caixa.
/// </summary>
public sealed class CashMovementRowViewModel
{
    public Guid Id { get; init; }
    public string KindLabel { get; init; } = string.Empty;
    public string WhenLabel { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string OperatorName { get; init; } = string.Empty;
    public string AmountLabel { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

/// <summary>
/// Total vendido em uma forma de pagamento no ciclo de caixa.
/// </summary>
public sealed class PaymentTotalRowViewModel
{
    public string Label { get; init; } = string.Empty;
    public string AmountLabel { get; init; } = string.Empty;
}

/// <summary>
/// Consulta e edição dos ciclos de caixa: período, saldos, sangrias e suprimentos.
/// </summary>
public sealed class CashSessionViewModel : ObservableObject
{
    private readonly CashRegisterService _cashRegisterService;
    private readonly ICashMovementPrinter _printer;
    private readonly ICurrentSession _session;
    private readonly Action<string>? _notify;
    private readonly List<CashSessionListItemDto> _allSessions = [];
    private bool _suppressSelection;
    private bool _suppressRange;
    private bool _rangeInvalid;
    private int _detailVersion;
    private CashMovementRowViewModel? _pendingDelete;

    private CashSessionListItemDto? _selectedSession;
    private DateTime _fromDate = MonthStart(DateTime.Today);
    private DateTime _toDate = DateTime.Today;
    private string _message = "Selecione um ciclo de abertura e fechamento.";
    private string _openingAmountLabel = "—";
    private string _cashBalanceLabel = "—";
    private string _generalBalanceLabel = "—";
    private string _totalSoldLabel = "—";
    private string _withdrawalsTotalLabel = "—";
    private string _suppliesTotalLabel = "—";
    private bool _canEditMovements;
    private bool _hasSession;
    private bool _showWithdrawalForm;
    private bool _showSupplyForm;
    private bool _showDeleteConfirm;
    private decimal _withdrawalAmount;
    private string _withdrawalReason = "Retirada de numerário";
    private decimal _supplyAmount;
    private string _supplyReason = "Reforço de troco";
    private string _deleteConfirmText = string.Empty;
    private string _thermalPreviewText = string.Empty;
    private bool _showThermalPreview;
    private bool _showA4Preview;
    private CashMovementReport? _previewReport;


    /// <summary>
    /// Liga os comandos de sangria, suprimento, exclusão, impressão e navegação de período.
    /// </summary>
    public CashSessionViewModel(
        CashRegisterService cashRegisterService,
        ICashMovementPrinter printer,
        ICurrentSession session,
        Action<string>? notify = null)
    {
        _cashRegisterService = cashRegisterService;
        _printer = printer;
        _session = session;
        _notify = notify;

        BeginWithdrawalCommand = new RelayCommand(_ => BeginWithdrawal(), _ => CanEditMovements);
        CancelWithdrawalCommand = new RelayCommand(_ => ShowWithdrawalForm = false);
        ConfirmWithdrawalCommand = new RelayCommand(async _ => await ConfirmMovementAsync(CashMovementType.Withdrawal), _ => CanEditMovements);
        BeginSupplyCommand = new RelayCommand(_ => BeginSupply(), _ => CanEditMovements);
        CancelSupplyCommand = new RelayCommand(_ => ShowSupplyForm = false);
        ConfirmSupplyCommand = new RelayCommand(async _ => await ConfirmMovementAsync(CashMovementType.Supply), _ => CanEditMovements);
        BeginDeleteCommand = new RelayCommand(p => BeginDelete(p as CashMovementRowViewModel), _ => CanEditMovements);
        ConfirmDeleteCommand = new RelayCommand(async _ => await ConfirmDeleteAsync(), _ => CanEditMovements && _pendingDelete is not null);
        CancelDeleteCommand = new RelayCommand(_ => CloseDeleteConfirm());
        CurrentMonthCommand = new RelayCommand(_ => ApplyCurrentMonth());
        GoToOpenSessionCommand = new RelayCommand(_ => GoToOpenSession(), _ => _allSessions.Any(s => s.IsOpen));
        PrintThermalCommand = new RelayCommand(async _ => await OpenThermalPreviewAsync(), _ => HasSession);
        PrintA4Command = new RelayCommand(async _ => await OpenA4PreviewAsync(), _ => HasSession);
        ClosePreviewCommand = new RelayCommand(_ => ClosePreview());
        ConfirmThermalPrintCommand = new RelayCommand(async _ => await ConfirmThermalPrintAsync(), _ => _previewReport is not null);
        ConfirmA4PrintCommand = new RelayCommand(async _ => await ConfirmA4PrintAsync(), _ => _previewReport is not null);
    }

    public ObservableCollection<CashSessionListItemDto> Sessions { get; } = [];
    public ObservableCollection<CashMovementRowViewModel> Withdrawals { get; } = [];
    public ObservableCollection<CashMovementRowViewModel> Supplies { get; } = [];
    public ObservableCollection<PaymentTotalRowViewModel> PaymentTotals { get; } = [];

    public RelayCommand BeginWithdrawalCommand { get; }
    public RelayCommand CancelWithdrawalCommand { get; }
    public RelayCommand ConfirmWithdrawalCommand { get; }
    public RelayCommand BeginSupplyCommand { get; }
    public RelayCommand CancelSupplyCommand { get; }
    public RelayCommand ConfirmSupplyCommand { get; }
    public RelayCommand BeginDeleteCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }
    public RelayCommand CurrentMonthCommand { get; }
    public RelayCommand GoToOpenSessionCommand { get; }
    public RelayCommand PrintThermalCommand { get; }
    public RelayCommand PrintA4Command { get; }
    public RelayCommand ClosePreviewCommand { get; }
    public RelayCommand ConfirmThermalPrintCommand { get; }
    public RelayCommand ConfirmA4PrintCommand { get; }

    public ObservableCollection<BitmapImage> A4PreviewPages { get; } = [];

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetRangeDate(ref _fromDate, value, nameof(FromDate));
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetRangeDate(ref _toDate, value, nameof(ToDate));
    }

    public string OpenSessionVisibility => OpenSessionOutsideRange() ? "Visible" : "Collapsed";

    public string EmptySessionsVisibility => Sessions.Count == 0 ? "Visible" : "Collapsed";

    public string EmptySessionsText => _allSessions.Count == 0
        ? "Nenhum caixa foi aberto neste terminal."
        : "Nenhum ciclo neste período.";

    public CashSessionListItemDto? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (_suppressSelection)
                return;

            if (EqualityComparer<CashSessionListItemDto?>.Default.Equals(_selectedSession, value))
                return;

            _selectedSession = value;
            RaisePropertyChanged();
            _ = LoadDetailAsync();
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string OpeningAmountLabel
    {
        get => _openingAmountLabel;
        private set => SetProperty(ref _openingAmountLabel, value);
    }

    public string CashBalanceLabel
    {
        get => _cashBalanceLabel;
        private set => SetProperty(ref _cashBalanceLabel, value);
    }

    public string GeneralBalanceLabel
    {
        get => _generalBalanceLabel;
        private set => SetProperty(ref _generalBalanceLabel, value);
    }

    public string TotalSoldLabel
    {
        get => _totalSoldLabel;
        private set => SetProperty(ref _totalSoldLabel, value);
    }

    public string WithdrawalsTotalLabel
    {
        get => _withdrawalsTotalLabel;
        private set => SetProperty(ref _withdrawalsTotalLabel, value);
    }

    public string SuppliesTotalLabel
    {
        get => _suppliesTotalLabel;
        private set => SetProperty(ref _suppliesTotalLabel, value);
    }

    public bool CanEditMovements
    {
        get => _canEditMovements;
        private set
        {
            if (_canEditMovements == value)
            {
                RaisePropertyChanged(nameof(ReadOnlyHintVisibility));
                return;
            }

            SetProperty(ref _canEditMovements, value);
            RaisePropertyChanged(nameof(ReadOnlyHintVisibility));
            BeginWithdrawalCommand.RaiseCanExecuteChanged();
            ConfirmWithdrawalCommand.RaiseCanExecuteChanged();
            BeginSupplyCommand.RaiseCanExecuteChanged();
            ConfirmSupplyCommand.RaiseCanExecuteChanged();
            BeginDeleteCommand.RaiseCanExecuteChanged();
            ConfirmDeleteCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSession
    {
        get => _hasSession;
        private set
        {
            if (_hasSession == value)
                return;

            SetProperty(ref _hasSession, value);
            PrintThermalCommand.RaiseCanExecuteChanged();
            PrintA4Command.RaiseCanExecuteChanged();
        }
    }

    public bool ShowThermalPreview
    {
        get => _showThermalPreview;
        private set => SetProperty(ref _showThermalPreview, value);
    }

    public bool ShowA4Preview
    {
        get => _showA4Preview;
        private set => SetProperty(ref _showA4Preview, value);
    }

    public string ThermalPreviewText
    {
        get => _thermalPreviewText;
        private set => SetProperty(ref _thermalPreviewText, value);
    }

    public string ReadOnlyHintVisibility => HasSession && !CanEditMovements ? "Visible" : "Collapsed";
    public string EmptyWithdrawalsVisibility => Withdrawals.Count == 0 ? "Visible" : "Collapsed";
    public string EmptySuppliesVisibility => Supplies.Count == 0 ? "Visible" : "Collapsed";

    public bool ShowWithdrawalForm
    {
        get => _showWithdrawalForm;
        private set => SetProperty(ref _showWithdrawalForm, value);
    }

    public bool ShowSupplyForm
    {
        get => _showSupplyForm;
        private set => SetProperty(ref _showSupplyForm, value);
    }

    public bool ShowDeleteConfirm
    {
        get => _showDeleteConfirm;
        private set => SetProperty(ref _showDeleteConfirm, value);
    }

    public decimal WithdrawalAmount
    {
        get => _withdrawalAmount;
        set => SetProperty(ref _withdrawalAmount, value);
    }

    public string WithdrawalReason
    {
        get => _withdrawalReason;
        set => SetProperty(ref _withdrawalReason, value);
    }

    public decimal SupplyAmount
    {
        get => _supplyAmount;
        set => SetProperty(ref _supplyAmount, value);
    }

    public string SupplyReason
    {
        get => _supplyReason;
        set => SetProperty(ref _supplyReason, value);
    }

    public string DeleteConfirmText
    {
        get => _deleteConfirmText;
        private set => SetProperty(ref _deleteConfirmText, value);
    }

    /// <summary>
    /// Carrega todos os ciclos do terminal e aplica o período visível.
    /// </summary>
    public async Task LoadAsync(bool resetToCurrentMonth = false)
    {
        try
        {
            if (resetToCurrentMonth)
                SetCurrentMonthDates();

            var sessions = await _cashRegisterService.ListSessionsAsync();
            _allSessions.Clear();
            _allSessions.AddRange(sessions);
            GoToOpenSessionCommand.RaiseCanExecuteChanged();
            await ApplyRangeAsync();
        }
        catch (DomainException ex)
        {
            Publish(ex.Message);
        }
    }

    /// <summary>
    /// Volta o filtro para o mês corrente até hoje.
    /// </summary>
    private void ApplyCurrentMonth()
    {
        SetCurrentMonthDates();
        _ = ApplyRangeAsync();
    }

    /// <summary>
    /// Ajusta o período para o dia do caixa aberto e o seleciona.
    /// </summary>
    private void GoToOpenSession()
    {
        var open = _allSessions.FirstOrDefault(s => s.IsOpen);
        if (open is null)
            return;

        var day = ToLocal(open.OpenedAt).Date;
        _suppressRange = true;
        if (_fromDate != day)
        {
            _fromDate = day;
            RaisePropertyChanged(nameof(FromDate));
        }

        if (_toDate != day)
        {
            _toDate = day;
            RaisePropertyChanged(nameof(ToDate));
        }

        _suppressRange = false;
        _ = ApplyRangeAsync();
    }

    /// <summary>
    /// Define as datas do mês sem disparar o filtro a cada atribuição.
    /// </summary>
    private void SetCurrentMonthDates()
    {
        var today = DateTime.Today;
        _suppressRange = true;
        SetRangeDate(ref _fromDate, MonthStart(today), nameof(FromDate));
        SetRangeDate(ref _toDate, today, nameof(ToDate));
        _suppressRange = false;
    }

    /// <summary>
    /// Grava uma data do período e reaplica o filtro, salvo quando a atualização está suprimida.
    /// </summary>
    private void SetRangeDate(ref DateTime field, DateTime value, string propertyName)
    {
        var date = value.Date;
        if (field == date)
            return;

        field = date;
        RaisePropertyChanged(propertyName);
        if (!_suppressRange)
            _ = ApplyRangeAsync();
    }

    /// <summary>
    /// Filtra os ciclos pelo período e recarrega o detalhe, preservando a seleção quando possível.
    /// </summary>
    private async Task ApplyRangeAsync()
    {
        if (_toDate < _fromDate)
        {
            _rangeInvalid = true;
            Publish("A data final não pode ser menor que a inicial.");
            RaisePropertyChanged(nameof(OpenSessionVisibility));
            return;
        }

        var wasInvalid = _rangeInvalid;
        _rangeInvalid = false;

        var keepId = _selectedSession?.Id;
        var filtered = _allSessions.Where(IsInRange).ToList();
        var next = filtered.FirstOrDefault(s => s.Id == keepId)
            ?? filtered.FirstOrDefault(s => s.IsOpen)
            ?? filtered.FirstOrDefault();

        _suppressSelection = true;
        Sessions.Clear();
        foreach (var session in filtered)
            Sessions.Add(session);

        _selectedSession = next;
        RaisePropertyChanged(nameof(SelectedSession));
        _suppressSelection = false;
        RaisePropertyChanged(nameof(OpenSessionVisibility));
        RaisePropertyChanged(nameof(EmptySessionsVisibility));
        RaisePropertyChanged(nameof(EmptySessionsText));

        await LoadDetailAsync();
        if (wasInvalid)
            _notify?.Invoke(Message);
    }

    /// <summary>
    /// Indica se a abertura do ciclo cai dentro do período filtrado.
    /// </summary>
    private bool IsInRange(CashSessionListItemDto session)
    {
        var day = ToLocal(session.OpenedAt).Date;
        return day >= _fromDate && day <= _toDate;
    }

    /// <summary>
    /// Indica se existe caixa aberto fora do período, para oferecer o atalho até ele.
    /// </summary>
    private bool OpenSessionOutsideRange()
    {
        var open = _allSessions.FirstOrDefault(s => s.IsOpen);
        if (open is null)
            return false;

        if (_toDate < _fromDate)
            return true;

        return !IsInRange(open);
    }

    /// <summary>
    /// Primeiro dia do mês da data informada.
    /// </summary>
    private static DateTime MonthStart(DateTime day) => new(day.Year, day.Month, 1);

    /// <summary>
    /// Converte o instante para o fuso local. Valores sem fuso são tratados como UTC.
    /// </summary>
    private static DateTime ToLocal(DateTime value)
        => value.Kind == DateTimeKind.Local
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();

    /// <summary>
    /// Carrega saldos e movimentações do ciclo selecionado. Respostas antigas são descartadas.
    /// </summary>
    private async Task LoadDetailAsync()
    {
        var version = ++_detailVersion;
        CloseForms();
        if (_selectedSession is null)
        {
            ClearDetail();
            SetScreenMessage(_allSessions.Count == 0
                ? "Nenhum caixa foi aberto neste terminal."
                : Sessions.Count == 0
                    ? "Nenhum ciclo neste período."
                    : "Selecione um ciclo de abertura e fechamento.");
            return;
        }

        try
        {
            var sessionId = _selectedSession.Id;
            var detail = await _cashRegisterService.GetSessionDetailAsync(sessionId);
            if (version != _detailVersion)
                return;

            if (detail is null)
            {
                ClearDetail();
                SetScreenMessage("Ciclo não encontrado.");
                return;
            }

            ApplyDetail(detail);
            SetScreenMessage(detail.IsOpen
                ? "Caixa aberto. Sangrias e suprimentos podem ser incluídos ou excluídos."
                : "Ciclo fechado. Consulta apenas.");
        }
        catch (DomainException ex)
        {
            if (version == _detailVersion)
                Publish(ex.Message);
        }
    }

    /// <summary>
    /// Preenche rótulos, listas e a permissão de edição a partir do detalhe do ciclo.
    /// </summary>
    private void ApplyDetail(CashSessionDetailDto detail)
    {
        HasSession = true;
        CanEditMovements = detail.IsOpen;
        OpeningAmountLabel = detail.OpeningAmount.ToString("C");
        CashBalanceLabel = detail.CashBalance.ToString("C");
        GeneralBalanceLabel = detail.GeneralBalance.ToString("C");
        TotalSoldLabel = detail.TotalSold.ToString("C");
        PaymentTotals.Clear();
        foreach (var total in detail.PaymentTotals)
        {
            PaymentTotals.Add(new PaymentTotalRowViewModel
            {
                Label = total.Label,
                AmountLabel = total.Amount.ToString("C")
            });
        }
        WithdrawalsTotalLabel = detail.Withdrawals.ToString("C");
        SuppliesTotalLabel = detail.Supplies.ToString("C");

        Withdrawals.Clear();
        foreach (var movement in detail.WithdrawalsList)
            Withdrawals.Add(ToRow(movement, "Sangria"));

        Supplies.Clear();
        foreach (var movement in detail.SuppliesList)
            Supplies.Add(ToRow(movement, "Suprimento"));

        RaisePropertyChanged(nameof(EmptyWithdrawalsVisibility));
        RaisePropertyChanged(nameof(EmptySuppliesVisibility));
        RaisePropertyChanged(nameof(ReadOnlyHintVisibility));
    }

    /// <summary>
    /// Zera o detalhe quando nenhum ciclo está selecionado.
    /// </summary>
    private void ClearDetail()
    {
        HasSession = false;
        CanEditMovements = false;
        OpeningAmountLabel = "—";
        CashBalanceLabel = "—";
        GeneralBalanceLabel = "—";
        TotalSoldLabel = "—";
        PaymentTotals.Clear();
        WithdrawalsTotalLabel = "—";
        SuppliesTotalLabel = "—";
        Withdrawals.Clear();
        Supplies.Clear();
        RaisePropertyChanged(nameof(EmptyWithdrawalsVisibility));
        RaisePropertyChanged(nameof(EmptySuppliesVisibility));
        RaisePropertyChanged(nameof(ReadOnlyHintVisibility));
    }

    /// <summary>
    /// Converte uma movimentação do serviço para a linha exibida na grade.
    /// </summary>
    private static CashMovementRowViewModel ToRow(CashSessionMovementDto movement, string kindLabel)
    {
        var when = movement.OccurredAt.Kind == DateTimeKind.Local
            ? movement.OccurredAt
            : DateTime.SpecifyKind(movement.OccurredAt, DateTimeKind.Utc).ToLocalTime();

        return new CashMovementRowViewModel
        {
            Id = movement.Id,
            KindLabel = kindLabel,
            WhenLabel = when.ToString("HH:mm"),
            Reason = movement.Reason,
            OperatorName = movement.OperatorName,
            Amount = movement.Amount,
            AmountLabel = movement.Amount.ToString("C")
        };
    }

    /// <summary>
    /// Abre o formulário de sangria com o motivo padrão.
    /// </summary>
    private void BeginWithdrawal()
    {
        if (!CanEditMovements)
            return;

        ShowSupplyForm = false;
        WithdrawalAmount = 0;
        WithdrawalReason = "Retirada de numerário";
        ShowWithdrawalForm = true;
    }

    /// <summary>
    /// Abre o formulário de suprimento com o motivo padrão.
    /// </summary>
    private void BeginSupply()
    {
        if (!CanEditMovements)
            return;

        ShowWithdrawalForm = false;
        SupplyAmount = 0;
        SupplyReason = "Reforço de troco";
        ShowSupplyForm = true;
    }

    /// <summary>
    /// Valida valor e motivo e registra a sangria ou o suprimento no caixa aberto.
    /// </summary>
    private async Task ConfirmMovementAsync(CashMovementType type)
    {
        if (_selectedSession is null || !CanEditMovements)
            return;

        var amount = type == CashMovementType.Supply ? SupplyAmount : WithdrawalAmount;
        var reason = type == CashMovementType.Supply ? SupplyReason : WithdrawalReason;
        if (amount <= 0)
        {
            Publish("Valor da movimentação deve ser maior que zero.");
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            Publish("Motivo da movimentação é obrigatório.");
            return;
        }

        try
        {
            await _cashRegisterService.AddMovementAsync(type, amount, reason);
            var label = type == CashMovementType.Supply ? "Suprimento" : "Sangria";
            Publish($"{label} de {amount.ToString("C", CultureInfo.CurrentCulture)} registrada.");
            await LoadAsync();
        }
        catch (DomainException ex)
        {
            Publish(ex.Message);
        }
    }

    /// <summary>
    /// Pede confirmação antes de excluir a movimentação.
    /// </summary>
    private void BeginDelete(CashMovementRowViewModel? row)
    {
        if (row is null || !CanEditMovements)
            return;

        _pendingDelete = row;
        DeleteConfirmText = $"Excluir {row.KindLabel.ToLowerInvariant()} de {row.AmountLabel}? {row.Reason}";
        ShowDeleteConfirm = true;
        ConfirmDeleteCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Exclui a movimentação pendente e recarrega os ciclos.
    /// </summary>
    private async Task ConfirmDeleteAsync()
    {
        if (_pendingDelete is null || _selectedSession is null || !CanEditMovements)
            return;

        try
        {
            await _cashRegisterService.RemoveMovementAsync(_selectedSession.Id, _pendingDelete.Id);
            Publish($"{_pendingDelete.KindLabel} de {_pendingDelete.AmountLabel} excluída.");
            CloseDeleteConfirm();
            await LoadAsync();
        }
        catch (DomainException ex)
        {
            Publish(ex.Message);
        }
    }

    /// <summary>
    /// Fecha a confirmação de exclusão e descarta a movimentação pendente.
    /// </summary>
    private void CloseDeleteConfirm()
    {
        _pendingDelete = null;
        ShowDeleteConfirm = false;
        DeleteConfirmText = string.Empty;
        ConfirmDeleteCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Fecha os formulários de sangria, suprimento e exclusão.
    /// </summary>
    private void CloseForms()
    {
        ShowWithdrawalForm = false;
        ShowSupplyForm = false;
        CloseDeleteConfirm();
    }

    /// <summary>
    /// Abre o resumo da bobina no overlay, sem enviar à impressora.
    /// </summary>
    private async Task OpenThermalPreviewAsync()
    {
        var report = await LoadReportAsync();
        if (report is null)
            return;

        SetPreviewReport(report);
        ThermalPreviewText = new CashMovementTextFormatter().Format(report);
        ShowA4Preview = false;
        ShowThermalPreview = true;
    }

    /// <summary>
    /// Abre as páginas A4 na tela, sem diálogo de impressora.
    /// </summary>
    private async Task OpenA4PreviewAsync()
    {
        var report = await LoadReportAsync();
        if (report is null)
            return;

        try
        {
            var pages = _printer.RenderA4PreviewPngs(report);
            A4PreviewPages.Clear();
            foreach (var png in pages)
                A4PreviewPages.Add(LoadPng(png));

            SetPreviewReport(report);
            ShowThermalPreview = false;
            ShowA4Preview = true;
        }
        catch (DomainException ex)
        {
            Publish(ex.Message);
        }
        catch (Exception ex)
        {
            Publish($"Falha na pré-visualização: {ex.Message}");
        }
    }

    /// <summary>
    /// Envia o resumo já exibido para a impressora térmica padrão.
    /// </summary>
    private async Task ConfirmThermalPrintAsync()
    {
        if (_previewReport is null)
            return;

        try
        {
            await _printer.PrintThermalAsync(_previewReport);
            Publish("Movimento de caixa enviado para impressão.");
        }
        catch (DomainException ex)
        {
            Publish(ex.Message);
        }
        catch (Exception ex)
        {
            Publish($"Falha na impressão: {ex.Message}");
        }
    }

    /// <summary>
    /// Abre o diálogo de impressoras e envia o A4 já exibido.
    /// </summary>
    private async Task ConfirmA4PrintAsync()
    {
        if (_previewReport is null)
            return;

        try
        {
            var printed = await _printer.PrintA4Async(_previewReport);
            Publish(printed
                ? "Movimento de caixa enviado para impressão."
                : "Impressão cancelada.");
        }
        catch (DomainException ex)
        {
            Publish(ex.Message);
        }
        catch (Exception ex)
        {
            Publish($"Falha na impressão: {ex.Message}");
        }
    }

    /// <summary>
    /// Fecha a pré-visualização sem imprimir.
    /// </summary>
    public void ClosePreview()
    {
        ShowThermalPreview = false;
        ShowA4Preview = false;
        ThermalPreviewText = string.Empty;
        A4PreviewPages.Clear();
        SetPreviewReport(null);
    }

    private async Task<CashMovementReport?> LoadReportAsync()
    {
        if (_selectedSession is null)
            return null;

        try
        {
            var detail = await _cashRegisterService.GetSessionDetailAsync(_selectedSession.Id);
            if (detail is null)
            {
                Publish("Ciclo não encontrado.");
                return null;
            }

            var store = _session.CurrentStore;
            return new CashMovementReport(
                store?.Code ?? string.Empty,
                store?.Name ?? string.Empty,
                detail,
                DateTime.Now);
        }
        catch (DomainException ex)
        {
            Publish(ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            Publish($"Falha na pré-visualização: {ex.Message}");
            return null;
        }
    }

    private void SetPreviewReport(CashMovementReport? report)
    {
        _previewReport = report;
        ConfirmThermalPrintCommand.RaiseCanExecuteChanged();
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

    /// <summary>
    /// Atualiza a mensagem da tela sem ecoar no status geral.
    /// </summary>
    private void SetScreenMessage(string message) => Message = message;

    /// <summary>
    /// Atualiza a mensagem da tela e a repete no status da janela principal.
    /// </summary>
    private void Publish(string message)
    {
        Message = message;
        _notify?.Invoke(message);
    }
}
