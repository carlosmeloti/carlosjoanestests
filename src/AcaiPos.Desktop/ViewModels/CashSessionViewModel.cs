using System.Collections.ObjectModel;
using System.Globalization;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
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
/// Consulta e edição dos ciclos de caixa: período, saldos, sangrias e suprimentos.
/// </summary>
public sealed class CashSessionViewModel : ObservableObject
{
    private readonly CashRegisterService _cashRegisterService;
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
    private string _cashSalesLabel = "—";
    private string _debitSalesLabel = "—";
    private string _creditSalesLabel = "—";
    private string _pixSalesLabel = "—";
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

    /// <summary>
    /// Liga os comandos de sangria, suprimento, exclusão e navegação de período.
    /// </summary>
    public CashSessionViewModel(CashRegisterService cashRegisterService, Action<string>? notify = null)
    {
        _cashRegisterService = cashRegisterService;
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
    }

    public ObservableCollection<CashSessionListItemDto> Sessions { get; } = [];
    public ObservableCollection<CashMovementRowViewModel> Withdrawals { get; } = [];
    public ObservableCollection<CashMovementRowViewModel> Supplies { get; } = [];

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

    public string CashSalesLabel
    {
        get => _cashSalesLabel;
        private set => SetProperty(ref _cashSalesLabel, value);
    }

    public string DebitSalesLabel
    {
        get => _debitSalesLabel;
        private set => SetProperty(ref _debitSalesLabel, value);
    }

    public string CreditSalesLabel
    {
        get => _creditSalesLabel;
        private set => SetProperty(ref _creditSalesLabel, value);
    }

    public string PixSalesLabel
    {
        get => _pixSalesLabel;
        private set => SetProperty(ref _pixSalesLabel, value);
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
        private set => SetProperty(ref _hasSession, value);
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
        CashSalesLabel = detail.CashSales.ToString("C");
        DebitSalesLabel = detail.DebitSales.ToString("C");
        CreditSalesLabel = detail.CreditSales.ToString("C");
        PixSalesLabel = detail.PixSales.ToString("C");
        TotalSoldLabel = detail.TotalSold.ToString("C");
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
        CashSalesLabel = "—";
        DebitSalesLabel = "—";
        CreditSalesLabel = "—";
        PixSalesLabel = "—";
        TotalSoldLabel = "—";
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
