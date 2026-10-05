using System.Collections.ObjectModel;
using System.Windows;
using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Desktop.ViewModels;

public sealed class PaymentLineViewModel : ObservableObject
{
    public PaymentMethod Method { get; init; }
    public string MethodLabel { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal? AmountReceived { get; init; }
    public string AmountLabel => Amount.ToString("C");
}

public sealed class CartLineViewModel : ObservableObject
{
    private decimal _quantity;

    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ProductSku { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            SetProperty(ref _quantity, value);
            RaisePropertyChanged(nameof(Subtotal));
            RaisePropertyChanged(nameof(QuantityLabel));
        }
    }

    public decimal Subtotal => Math.Round(Quantity * UnitPrice, 2);
    public string QuantityLabel => Quantity % 1 == 0 ? ((int)Quantity).ToString() : Quantity.ToString("0.###");
    public string UnitPriceLabel => UnitPrice.ToString("C");
    public string SubtotalLabel => Subtotal.ToString("C");
}

public sealed class MainViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly CatalogService _catalogService;
    private readonly CashRegisterService _cashRegisterService;
    private readonly SaleService _saleService;
    private readonly TicketService _ticketService;
    private readonly UserService _userService;
    private readonly ICurrentSession _session;

    private bool _isAuthenticated;
    private bool _isCashOpen;
    private string _username = "operador";
    private string _pin = string.Empty;
    private string _statusMessage = "Entre com seu usuário e PIN para começar.";
    private string _operatorName = string.Empty;
    private string _terminalLabel = string.Empty;
    private string _connectionStatus = "OFFLINE-LOCAL";
    private string _clockText = DateTime.Now.ToString("HH:mm");
    private string _searchText = string.Empty;
    private Guid? _selectedCategoryId;
    private decimal _openingAmount = 100m;
    private decimal _countedAmount;
    private string _paymentPanelVisibility = "Collapsed";
    private decimal _cashReceived;
    private decimal _partialPaymentAmount;
    private decimal _discountAmount;
    private string _discountAuthorizerUsername = "gerente";
    private string _discountAuthorizerPin = string.Empty;
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Pix;
    private string _lastTicketInfo = string.Empty;
    private bool _showCashOpenPanel;
    private bool _showCashClosePanel;
    private bool _showCashMovementPanel;
    private bool _showTicketPanel;
    private bool _showReprintPanel;
    private string _ticketPreviewText = string.Empty;
    private Guid? _currentTicketSaleId;
    private string _ticketPanelTitle = "Ticket";
    private string _cashMovementTitle = "Movimentação de caixa";
    private CashMovementType _cashMovementType = CashMovementType.Withdrawal;
    private decimal _cashMovementAmount;
    private string _cashMovementReason = string.Empty;
    private string _cashSummaryText = string.Empty;
    private string _currentScreen = "Sale";
    private bool _canManageCatalog;
    private bool _canManageUsers;

    public MainViewModel(
        AuthService authService,
        CatalogService catalogService,
        CashRegisterService cashRegisterService,
        SaleService saleService,
        TicketService ticketService,
        UserService userService,
        ICurrentSession session)
    {
        _authService = authService;
        _catalogService = catalogService;
        _cashRegisterService = cashRegisterService;
        _saleService = saleService;
        _ticketService = ticketService;
        _userService = userService;
        _session = session;

        Catalog = new CatalogManagementViewModel(
            catalogService,
            msg => StatusMessage = msg,
            async () => await LoadCatalogAsync());

        Users = new UserManagementViewModel(
            userService,
            msg => StatusMessage = msg);

        History = new SalesHistoryViewModel(
            saleService,
            msg => StatusMessage = msg,
            async id => await ShowTicketAsync(id, isReprint: true),
            async id =>
            {
                await ShowTicketAsync(id, isReprint: true);
                await PrintCurrentTicketAsync();
            });

        LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => !IsAuthenticated);
        LogoutCommand = new RelayCommand(async _ => await LogoutAsync(), _ => IsAuthenticated);
        OpenCashCommand = new RelayCommand(_ => ShowCashOpenPanel = true, _ => IsAuthenticated && !IsCashOpen);
        ConfirmOpenCashCommand = new RelayCommand(async _ => await OpenCashAsync(), _ => IsAuthenticated && !IsCashOpen);
        CloseCashCommand = new RelayCommand(_ => BeginCloseCash(), _ => IsAuthenticated && IsCashOpen);
        ConfirmCloseCashCommand = new RelayCommand(async _ => await ConfirmCloseCashAsync(), _ => IsCashOpen);
        CancelCloseCashCommand = new RelayCommand(_ => ShowCashClosePanel = false);
        SelectCategoryCommand = new RelayCommand(async p =>
        {
            Guid? id = p is Guid guid ? guid : null;
            await SelectCategoryAsync(id);
        });
        AddProductCommand = new RelayCommand(p => AddProduct(p as ProductDto), _ => IsCashOpen);
        IncreaseItemCommand = new RelayCommand(p => ChangeQty(p as CartLineViewModel, 1), _ => IsCashOpen);
        DecreaseItemCommand = new RelayCommand(p => ChangeQty(p as CartLineViewModel, -1), _ => IsCashOpen);
        RemoveItemCommand = new RelayCommand(p => RemoveItem(p as CartLineViewModel), _ => IsCashOpen);
        ClearCartCommand = new RelayCommand(_ => ClearCart(), _ => Cart.Count > 0);
        SearchCommand = new RelayCommand(async _ => await LoadProductsAsync());
        SearchOrBarcodeCommand = new RelayCommand(async _ => await SearchOrBarcodeAsync());
        StartPaymentCommand = new RelayCommand(_ => StartPayment(), _ => IsCashOpen && Cart.Count > 0);
        CancelPaymentCommand = new RelayCommand(_ => CancelPaymentPanel());
        PayFullCommand = new RelayCommand(async p => await AddPaymentAsync(p, payRemaining: true), _ => Cart.Count > 0);
        AddPartialPaymentCommand = new RelayCommand(async p => await AddPaymentAsync(p, payRemaining: false), _ => Cart.Count > 0);
        ConfirmCashPaymentCommand = new RelayCommand(async _ => await AddPaymentAsync("Cash", payRemaining: false), _ => Cart.Count > 0);
        RemovePaymentLineCommand = new RelayCommand(p => RemovePaymentLine(p as PaymentLineViewModel));
        CompleteMixedPaymentCommand = new RelayCommand(async _ => await CompleteSaleAsync(PaymentLines.Select(ToDraft).ToList()), _ => PaymentRemaining <= 0.009m && PaymentLines.Count > 0);
        AppendPinCommand = new RelayCommand(p => AppendPin(p?.ToString()));
        ClearPinCommand = new RelayCommand(_ => Pin = string.Empty);
        BackspacePinCommand = new RelayCommand(_ =>
        {
            if (Pin.Length > 0)
                Pin = Pin[..^1];
        });
        GoToSaleCommand = new RelayCommand(async _ => await NavigateToSaleAsync(), _ => IsAuthenticated);
        GoToCatalogCommand = new RelayCommand(async _ => await NavigateToCatalogAsync(), _ => IsAuthenticated && CanManageCatalog);
        GoToUsersCommand = new RelayCommand(async _ => await NavigateToUsersAsync(), _ => IsAuthenticated && CanManageUsers);
        GoToHistoryCommand = new RelayCommand(async _ => await NavigateToHistoryAsync(), _ => IsAuthenticated);
        OpenSupplyCommand = new RelayCommand(async _ => await OpenMovementPanelAsync(CashMovementType.Supply), _ => IsCashOpen);
        OpenWithdrawalCommand = new RelayCommand(async _ => await OpenMovementPanelAsync(CashMovementType.Withdrawal), _ => IsCashOpen);
        CancelCashMovementCommand = new RelayCommand(_ => ShowCashMovementPanel = false);
        ConfirmCashMovementCommand = new RelayCommand(async _ => await ConfirmCashMovementAsync(), _ => IsCashOpen);
        CloseTicketCommand = new RelayCommand(_ => CloseTicketPanel());
        PrintTicketCommand = new RelayCommand(async _ => await PrintCurrentTicketAsync(), _ => _currentTicketSaleId.HasValue);
        OpenReprintCommand = new RelayCommand(async _ => await OpenReprintPanelAsync(), _ => IsAuthenticated);
        CloseReprintCommand = new RelayCommand(_ => ShowReprintPanel = false);
        ViewTicketCommand = new RelayCommand(async p => await ViewTicketAsync(p as TicketDto), _ => IsAuthenticated);
        ReprintSelectedCommand = new RelayCommand(async p => await ReprintTicketAsync(p as TicketDto), _ => IsAuthenticated);

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (_, _) => ClockText = DateTime.Now.ToString("HH:mm:ss");
        timer.Start();
    }

    public CatalogManagementViewModel Catalog { get; }
    public UserManagementViewModel Users { get; }
    public SalesHistoryViewModel History { get; }

    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<CartLineViewModel> Cart { get; } = [];
    public ObservableCollection<CashMovementDto> CashMovements { get; } = [];
    public ObservableCollection<TicketDto> TodayTickets { get; } = [];
    public ObservableCollection<PaymentLineViewModel> PaymentLines { get; } = [];

    public RelayCommand LoginCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand OpenCashCommand { get; }
    public RelayCommand ConfirmOpenCashCommand { get; }
    public RelayCommand CloseCashCommand { get; }
    public RelayCommand ConfirmCloseCashCommand { get; }
    public RelayCommand CancelCloseCashCommand { get; }
    public RelayCommand SelectCategoryCommand { get; }
    public RelayCommand AddProductCommand { get; }
    public RelayCommand IncreaseItemCommand { get; }
    public RelayCommand DecreaseItemCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand ClearCartCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand SearchOrBarcodeCommand { get; }
    public RelayCommand StartPaymentCommand { get; }
    public RelayCommand CancelPaymentCommand { get; }
    public RelayCommand PayFullCommand { get; }
    public RelayCommand AddPartialPaymentCommand { get; }
    public RelayCommand ConfirmCashPaymentCommand { get; }
    public RelayCommand RemovePaymentLineCommand { get; }
    public RelayCommand CompleteMixedPaymentCommand { get; }
    public RelayCommand AppendPinCommand { get; }
    public RelayCommand ClearPinCommand { get; }
    public RelayCommand BackspacePinCommand { get; }
    public RelayCommand GoToSaleCommand { get; }
    public RelayCommand GoToCatalogCommand { get; }
    public RelayCommand GoToUsersCommand { get; }
    public RelayCommand GoToHistoryCommand { get; }
    public RelayCommand OpenSupplyCommand { get; }
    public RelayCommand OpenWithdrawalCommand { get; }
    public RelayCommand CancelCashMovementCommand { get; }
    public RelayCommand ConfirmCashMovementCommand { get; }
    public RelayCommand CloseTicketCommand { get; }
    public RelayCommand PrintTicketCommand { get; }
    public RelayCommand OpenReprintCommand { get; }
    public RelayCommand CloseReprintCommand { get; }
    public RelayCommand ViewTicketCommand { get; }
    public RelayCommand ReprintSelectedCommand { get; }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            SetProperty(ref _isAuthenticated, value);
            RaisePropertyChanged(nameof(LoginVisibility));
            RaisePropertyChanged(nameof(WorkspaceVisibility));
            LoginCommand.RaiseCanExecuteChanged();
            LogoutCommand.RaiseCanExecuteChanged();
            OpenCashCommand.RaiseCanExecuteChanged();
            CloseCashCommand.RaiseCanExecuteChanged();
            GoToSaleCommand.RaiseCanExecuteChanged();
            GoToCatalogCommand.RaiseCanExecuteChanged();
            GoToUsersCommand.RaiseCanExecuteChanged();
            GoToHistoryCommand.RaiseCanExecuteChanged();
            OpenReprintCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CatalogButtonVisibility));
            RaisePropertyChanged(nameof(UsersButtonVisibility));
        }
    }

    public bool IsCashOpen
    {
        get => _isCashOpen;
        set
        {
            SetProperty(ref _isCashOpen, value);
            RaisePropertyChanged(nameof(CashStatusText));
            OpenCashCommand.RaiseCanExecuteChanged();
            CloseCashCommand.RaiseCanExecuteChanged();
            OpenSupplyCommand.RaiseCanExecuteChanged();
            OpenWithdrawalCommand.RaiseCanExecuteChanged();
            ConfirmCashMovementCommand.RaiseCanExecuteChanged();
            AddProductCommand.RaiseCanExecuteChanged();
            StartPaymentCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CashMovementButtonsVisibility));
        }
    }

    public string LoginVisibility => IsAuthenticated ? "Collapsed" : "Visible";
    public string WorkspaceVisibility => IsAuthenticated ? "Visible" : "Collapsed";
    public string CashStatusText => IsCashOpen ? "CAIXA ABERTO" : "CAIXA FECHADO";
    public string SaleScreenVisibility => CurrentScreen == "Sale" ? "Visible" : "Collapsed";
    public string CatalogScreenVisibility => CurrentScreen == "Catalog" ? "Visible" : "Collapsed";
    public string UsersScreenVisibility => CurrentScreen == "Users" ? "Visible" : "Collapsed";
    public string HistoryScreenVisibility => CurrentScreen == "History" ? "Visible" : "Collapsed";

    public string CurrentScreen
    {
        get => _currentScreen;
        set
        {
            SetProperty(ref _currentScreen, value);
            RaisePropertyChanged(nameof(SaleScreenVisibility));
            RaisePropertyChanged(nameof(CatalogScreenVisibility));
            RaisePropertyChanged(nameof(UsersScreenVisibility));
            RaisePropertyChanged(nameof(HistoryScreenVisibility));
        }
    }

    public bool CanManageCatalog
    {
        get => _canManageCatalog;
        set
        {
            SetProperty(ref _canManageCatalog, value);
            GoToCatalogCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CatalogButtonVisibility));
        }
    }

    public bool CanManageUsers
    {
        get => _canManageUsers;
        set
        {
            SetProperty(ref _canManageUsers, value);
            GoToUsersCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(UsersButtonVisibility));
        }
    }

    public string CatalogButtonVisibility => CanManageCatalog ? "Visible" : "Collapsed";
    public string UsersButtonVisibility => CanManageUsers ? "Visible" : "Collapsed";
    public string CashMovementButtonsVisibility => IsCashOpen ? "Visible" : "Collapsed";
    public string DiscountAuthVisibility =>
        DiscountAmount > 0 && !_saleService.CurrentUserCanApplyDiscount ? "Visible" : "Collapsed";

    public bool ShowCashMovementPanel
    {
        get => _showCashMovementPanel;
        set => SetProperty(ref _showCashMovementPanel, value);
    }

    public string CashMovementTitle
    {
        get => _cashMovementTitle;
        set => SetProperty(ref _cashMovementTitle, value);
    }

    public decimal CashMovementAmount
    {
        get => _cashMovementAmount;
        set => SetProperty(ref _cashMovementAmount, value);
    }

    public string CashMovementReason
    {
        get => _cashMovementReason;
        set => SetProperty(ref _cashMovementReason, value);
    }

    public string CashSummaryText
    {
        get => _cashSummaryText;
        set => SetProperty(ref _cashSummaryText, value);
    }

    public bool ShowTicketPanel
    {
        get => _showTicketPanel;
        set => SetProperty(ref _showTicketPanel, value);
    }

    public bool ShowReprintPanel
    {
        get => _showReprintPanel;
        set => SetProperty(ref _showReprintPanel, value);
    }

    public string TicketPreviewText
    {
        get => _ticketPreviewText;
        set => SetProperty(ref _ticketPreviewText, value);
    }

    public string TicketPanelTitle
    {
        get => _ticketPanelTitle;
        set => SetProperty(ref _ticketPanelTitle, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Pin
    {
        get => _pin;
        set => SetProperty(ref _pin, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string OperatorName
    {
        get => _operatorName;
        set => SetProperty(ref _operatorName, value);
    }

    public string TerminalLabel
    {
        get => _terminalLabel;
        set => SetProperty(ref _terminalLabel, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public string ClockText
    {
        get => _clockText;
        set => SetProperty(ref _clockText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public decimal OpeningAmount
    {
        get => _openingAmount;
        set => SetProperty(ref _openingAmount, value);
    }

    public decimal CountedAmount
    {
        get => _countedAmount;
        set => SetProperty(ref _countedAmount, value);
    }

    public string PaymentPanelVisibility
    {
        get => _paymentPanelVisibility;
        set => SetProperty(ref _paymentPanelVisibility, value);
    }

    public decimal CashReceived
    {
        get => _cashReceived;
        set
        {
            SetProperty(ref _cashReceived, value);
            RaisePropertyChanged(nameof(CashChangeLabel));
        }
    }

    public decimal PartialPaymentAmount
    {
        get => _partialPaymentAmount;
        set => SetProperty(ref _partialPaymentAmount, value);
    }

    public decimal DiscountAmount
    {
        get => _discountAmount;
        set
        {
            var capped = Math.Max(0, Math.Min(value, CartTotal));
            SetProperty(ref _discountAmount, decimal.Round(capped, 2));
            RaisePaymentFacts();
            RaisePropertyChanged(nameof(DiscountAuthVisibility));
        }
    }

    public string DiscountAuthorizerUsername
    {
        get => _discountAuthorizerUsername;
        set => SetProperty(ref _discountAuthorizerUsername, value);
    }

    public string DiscountAuthorizerPin
    {
        get => _discountAuthorizerPin;
        set => SetProperty(ref _discountAuthorizerPin, value);
    }

    public PaymentMethod SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set => SetProperty(ref _selectedPaymentMethod, value);
    }

    public decimal PayableTotal => Math.Max(0, CartTotal - DiscountAmount);
    public decimal PaymentPaid => PaymentLines.Sum(p => p.Amount);
    public decimal PaymentRemaining => Math.Max(0, PayableTotal - PaymentPaid);
    public string PayableTotalLabel => PayableTotal.ToString("C");
    public string PaymentPaidLabel => PaymentPaid.ToString("C");
    public string PaymentRemainingLabel => PaymentRemaining.ToString("C");
    public string CartTotalLabel => CartTotal.ToString("C");
    public string CartCountLabel => $"{Cart.Sum(i => i.Quantity):0.###} itens";
    public decimal CartTotal => Cart.Sum(i => i.Subtotal);
    public string CashChangeLabel
    {
        get
        {
            if (PartialPaymentAmount <= 0)
                return "Informe o valor da parcela";
            var change = CashReceived - PartialPaymentAmount;
            return change >= 0 ? $"Troco desta parcela: {change:C}" : "Valor recebido insuficiente";
        }
    }

    public string LastTicketInfo
    {
        get => _lastTicketInfo;
        set => SetProperty(ref _lastTicketInfo, value);
    }

    public bool ShowCashOpenPanel
    {
        get => _showCashOpenPanel;
        set => SetProperty(ref _showCashOpenPanel, value);
    }

    public bool ShowCashClosePanel
    {
        get => _showCashClosePanel;
        set => SetProperty(ref _showCashClosePanel, value);
    }

    private async Task LoginAsync()
    {
        try
        {
            var result = await _authService.LoginAsync(Username, Pin);
            IsAuthenticated = true;
            IsCashOpen = result.HasOpenCashRegister;
            CanManageCatalog = result.Role is UserRole.Manager or UserRole.Administrator;
            CanManageUsers = result.Role is UserRole.Administrator;
            OperatorName = result.Name;
            TerminalLabel = $"{result.StoreName} · {result.TerminalCode}";
            Pin = string.Empty;
            CurrentScreen = "Sale";
            StatusMessage = IsCashOpen
                ? "Caixa já estava aberto. Pronto para vender."
                : "Login ok. Abra o caixa para iniciar as vendas.";
            ShowCashOpenPanel = !IsCashOpen;
            await LoadCatalogAsync();
        }
        catch (DomainException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao entrar: {ex.Message}";
        }
    }

    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        IsAuthenticated = false;
        IsCashOpen = false;
        CanManageCatalog = false;
        CanManageUsers = false;
        CurrentScreen = "Sale";
        Cart.Clear();
        PaymentLines.Clear();
        DiscountAmount = 0;
        Categories.Clear();
        Products.Clear();
        OperatorName = string.Empty;
        TerminalLabel = string.Empty;
        StatusMessage = "Sessão encerrada.";
        PaymentPanelVisibility = "Collapsed";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        ShowTicketPanel = false;
        ShowReprintPanel = false;
        TodayTickets.Clear();
        CashMovements.Clear();
        RefreshCartTotals();
    }

    private async Task NavigateToSaleAsync()
    {
        CurrentScreen = "Sale";
        await LoadCatalogAsync();
        StatusMessage = "Tela de venda.";
    }

    private async Task NavigateToCatalogAsync()
    {
        if (!CanManageCatalog)
        {
            StatusMessage = "Sem permissão para gerenciar o catálogo.";
            return;
        }

        CurrentScreen = "Catalog";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        PaymentPanelVisibility = "Collapsed";
        await Catalog.LoadAsync();
        StatusMessage = "Cadastro de produtos e categorias.";
    }

    private async Task NavigateToUsersAsync()
    {
        if (!CanManageUsers)
        {
            StatusMessage = "Sem permissão para gerenciar usuários.";
            return;
        }

        CurrentScreen = "Users";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        ShowTicketPanel = false;
        ShowReprintPanel = false;
        PaymentPanelVisibility = "Collapsed";
        await Users.LoadAsync();
        StatusMessage = "Cadastro de usuários.";
    }

    private async Task NavigateToHistoryAsync()
    {
        CurrentScreen = "History";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        ShowTicketPanel = false;
        ShowReprintPanel = false;
        PaymentPanelVisibility = "Collapsed";
        await History.InitializeAsync();
        StatusMessage = "Histórico de vendas.";
    }

    private async Task OpenMovementPanelAsync(CashMovementType type)
    {
        if (!IsCashOpen)
        {
            StatusMessage = "Abra o caixa antes de movimentar.";
            return;
        }

        _cashMovementType = type;
        CashMovementTitle = type == CashMovementType.Supply ? "Suprimento" : "Sangria";
        CashMovementAmount = 0;
        CashMovementReason = type == CashMovementType.Supply
            ? "Reforço de troco"
            : "Retirada de numerário";
        ShowCashClosePanel = false;
        ShowCashOpenPanel = false;
        PaymentPanelVisibility = "Collapsed";
        ShowCashMovementPanel = true;
        await RefreshCashMovementContextAsync();
    }

    private async Task ConfirmCashMovementAsync()
    {
        try
        {
            await _cashRegisterService.AddMovementAsync(
                _cashMovementType,
                CashMovementAmount,
                CashMovementReason);

            var label = _cashMovementType == CashMovementType.Supply ? "Suprimento" : "Sangria";
            StatusMessage = $"{label} de {CashMovementAmount:C} registrada.";
            ShowCashMovementPanel = false;
            await RefreshCashMovementContextAsync();
        }
        catch (DomainException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro na movimentação: {ex.Message}";
        }
    }

    private async Task RefreshCashMovementContextAsync()
    {
        CashMovements.Clear();
        foreach (var movement in await _cashRegisterService.GetOpenMovementsAsync())
            CashMovements.Add(movement);

        var summary = await _cashRegisterService.GetOpenSummaryAsync();
        if (summary is null)
        {
            CashSummaryText = string.Empty;
            return;
        }

        CashSummaryText =
            $"Abertura {summary.OpeningAmount:C} · Suprimentos {summary.Supplies:C} · " +
            $"Sangrias {summary.Withdrawals:C} · Esperado em dinheiro {summary.ExpectedCash:C}";
    }

    private async Task OpenCashAsync()
    {
        try
        {
            await _cashRegisterService.OpenAsync(OpeningAmount);
            IsCashOpen = true;
            ShowCashOpenPanel = false;
            StatusMessage = $"Caixa aberto com fundo de {OpeningAmount:C}.";
        }
        catch (DomainException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async void BeginCloseCash()
    {
        var summary = await _cashRegisterService.GetOpenSummaryAsync();
        if (summary is null)
            return;

        CountedAmount = summary.ExpectedCash;
        CashSummaryText =
            $"Abertura {summary.OpeningAmount:C} · Vendas dinheiro {summary.CashSales:C} · " +
            $"Suprimentos {summary.Supplies:C} · Sangrias {summary.Withdrawals:C} · Esperado {summary.ExpectedCash:C}";
        ShowCashMovementPanel = false;
        ShowCashClosePanel = true;
        StatusMessage = $"Esperado em dinheiro: {summary.ExpectedCash:C}";
    }

    private async Task ConfirmCloseCashAsync()
    {
        try
        {
            var result = await _cashRegisterService.CloseAsync(CountedAmount);
            IsCashOpen = false;
            ShowCashClosePanel = false;
            Cart.Clear();
            RefreshCartTotals();
            StatusMessage =
                $"Caixa fechado. Diferença: {result.DifferenceAmount:C} (contado {result.CountedAmount:C} / esperado {result.ExpectedAmount:C}).";
        }
        catch (DomainException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task LoadCatalogAsync()
    {
        Categories.Clear();
        Categories.Add(new CategoryDto(Guid.Empty, "Todos", null, 0));
        foreach (var category in await _catalogService.GetActiveCategoriesAsync())
            Categories.Add(category);

        _selectedCategoryId = null;
        await LoadProductsAsync();
    }

    private async Task SelectCategoryAsync(Guid? categoryId)
    {
        _selectedCategoryId = categoryId is null || categoryId == Guid.Empty ? null : categoryId;
        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        Products.Clear();
        var items = await _catalogService.GetSellableProductsAsync(_selectedCategoryId, SearchText);
        foreach (var item in items)
            Products.Add(item);
    }

    private void AddProduct(ProductDto? product)
    {
        if (product is null)
            return;

        if (!IsCashOpen)
        {
            StatusMessage = "Abra o caixa antes de vender.";
            ShowCashOpenPanel = true;
            return;
        }

        var existing = Cart.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += 1;
        }
        else
        {
            Cart.Add(new CartLineViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSku = product.Sku,
                UnitPrice = product.SalePrice,
                Quantity = 1
            });
        }

        RefreshCartTotals();
        StatusMessage = $"{product.Name} adicionado.";
    }

    private void ChangeQty(CartLineViewModel? item, decimal delta)
    {
        if (item is null)
            return;

        var next = item.Quantity + delta;
        if (next <= 0)
            Cart.Remove(item);
        else
            item.Quantity = next;

        RefreshCartTotals();
    }

    private void RemoveItem(CartLineViewModel? item)
    {
        if (item is null)
            return;

        Cart.Remove(item);
        RefreshCartTotals();
    }

    private void ClearCart()
    {
        Cart.Clear();
        DiscountAmount = 0;
        PaymentLines.Clear();
        RefreshCartTotals();
        PaymentPanelVisibility = "Collapsed";
    }

    private void CancelPaymentPanel()
    {
        PaymentLines.Clear();
        PaymentPanelVisibility = "Collapsed";
        RaisePaymentFacts();
    }

    private void StartPayment()
    {
        PaymentLines.Clear();
        DiscountAmount = Math.Min(DiscountAmount, CartTotal);
        PartialPaymentAmount = PayableTotal;
        CashReceived = PayableTotal;
        PaymentPanelVisibility = "Visible";
        RaisePaymentFacts();
    }

    private async Task AddPaymentAsync(object? parameter, bool payRemaining)
    {
        if (parameter is string methodText && Enum.TryParse<PaymentMethod>(methodText, out var method))
            SelectedPaymentMethod = method;

        var amount = payRemaining ? PaymentRemaining : PartialPaymentAmount;
        if (amount <= 0)
        {
            StatusMessage = "Informe um valor de pagamento válido.";
            return;
        }

        if (amount - PaymentRemaining > 0.009m)
        {
            StatusMessage = "Valor maior que o restante da venda.";
            return;
        }

        decimal? received = null;
        if (SelectedPaymentMethod == PaymentMethod.Cash)
        {
            received = payRemaining ? Math.Max(CashReceived, amount) : CashReceived;
            if ((received ?? 0) < amount)
            {
                StatusMessage = "Valor recebido insuficiente para a parcela em dinheiro.";
                return;
            }
        }

        PaymentLines.Add(new PaymentLineViewModel
        {
            Method = SelectedPaymentMethod,
            MethodLabel = MethodLabel(SelectedPaymentMethod),
            Amount = decimal.Round(amount, 2),
            AmountReceived = received.HasValue ? decimal.Round(received.Value, 2) : null
        });

        RaisePaymentFacts();
        PartialPaymentAmount = PaymentRemaining;
        CashReceived = PaymentRemaining;

        if (PaymentRemaining <= 0.009m)
            await CompleteSaleAsync(PaymentLines.Select(ToDraft).ToList());
    }

    private void RemovePaymentLine(PaymentLineViewModel? line)
    {
        if (line is null)
            return;
        PaymentLines.Remove(line);
        RaisePaymentFacts();
        PartialPaymentAmount = PaymentRemaining;
    }

    private static PaymentDraftDto ToDraft(PaymentLineViewModel line)
        => new(line.Method, line.Amount, line.AmountReceived);

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.DebitCard => "Débito",
        PaymentMethod.CreditCard => "Crédito",
        PaymentMethod.Pix => "PIX",
        _ => method.ToString()
    };

    private async Task CompleteSaleAsync(IReadOnlyList<PaymentDraftDto> payments)
    {
        try
        {
            var cart = Cart.Select(c => new CartItemDto(
                c.ProductId,
                c.ProductName,
                c.ProductSku,
                c.Quantity,
                c.UnitPrice,
                c.Subtotal)).ToList();

            var sale = await _saleService.CompleteSaleAsync(
                cart,
                payments,
                DiscountAmount,
                DiscountAmount > 0 && !_saleService.CurrentUserCanApplyDiscount ? DiscountAuthorizerUsername : null,
                DiscountAmount > 0 && !_saleService.CurrentUserCanApplyDiscount ? DiscountAuthorizerPin : null);

            LastTicketInfo =
                $"Ticket {sale.TicketNumber} · {sale.Total:C}" +
                (sale.ChangeAmount > 0 ? $" · Troco {sale.ChangeAmount:C}" : string.Empty);

            Cart.Clear();
            PaymentLines.Clear();
            DiscountAmount = 0;
            DiscountAuthorizerPin = string.Empty;
            RefreshCartTotals();
            PaymentPanelVisibility = "Collapsed";
            StatusMessage = $"Venda concluída: {LastTicketInfo}";
            await LoadProductsAsync();
            await ShowTicketAsync(sale.Id, isReprint: false);
        }
        catch (DomainException ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "AcaiPos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "AcaiPos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SearchOrBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadProductsAsync();
            return;
        }

        var byBarcode = await _catalogService.FindByBarcodeAsync(SearchText.Trim());
        if (byBarcode is not null)
        {
            AddProduct(byBarcode);
            SearchText = string.Empty;
            await LoadProductsAsync();
            return;
        }

        await LoadProductsAsync();
    }

    private void AppendPin(string? digit)
    {
        if (string.IsNullOrWhiteSpace(digit) || Pin.Length >= 8)
            return;

        Pin += digit;
    }

    private void RaisePaymentFacts()
    {
        RaisePropertyChanged(nameof(PayableTotal));
        RaisePropertyChanged(nameof(PayableTotalLabel));
        RaisePropertyChanged(nameof(PaymentPaid));
        RaisePropertyChanged(nameof(PaymentPaidLabel));
        RaisePropertyChanged(nameof(PaymentRemaining));
        RaisePropertyChanged(nameof(PaymentRemainingLabel));
        RaisePropertyChanged(nameof(CashChangeLabel));
        RaisePropertyChanged(nameof(DiscountAuthVisibility));
        CompleteMixedPaymentCommand.RaiseCanExecuteChanged();
    }

    private void RefreshCartTotals()
    {
        RaisePropertyChanged(nameof(CartTotal));
        RaisePropertyChanged(nameof(CartTotalLabel));
        RaisePropertyChanged(nameof(CartCountLabel));
        RaisePaymentFacts();
        StartPaymentCommand.RaiseCanExecuteChanged();
        ClearCartCommand.RaiseCanExecuteChanged();
    }

    private async Task ShowTicketAsync(Guid saleId, bool isReprint)
    {
        var ticket = await _ticketService.GetTicketAsync(saleId, isReprint);
        _currentTicketSaleId = ticket.SaleId;
        TicketPanelTitle = isReprint ? $"Reimpressão · {ticket.TicketNumber}" : $"Ticket · {ticket.TicketNumber}";
        TicketPreviewText = _ticketService.Format(ticket);
        ShowReprintPanel = false;
        ShowTicketPanel = true;
        PrintTicketCommand.RaiseCanExecuteChanged();
        LastTicketInfo = $"Ticket {ticket.TicketNumber} · {ticket.Total:C}";
    }

    private void CloseTicketPanel()
    {
        ShowTicketPanel = false;
        _currentTicketSaleId = null;
        TicketPreviewText = string.Empty;
        PrintTicketCommand.RaiseCanExecuteChanged();
    }

    private async Task PrintCurrentTicketAsync()
    {
        if (!_currentTicketSaleId.HasValue)
            return;

        try
        {
            var isReprint = TicketPanelTitle.StartsWith("Reimpressão", StringComparison.OrdinalIgnoreCase);
            await _ticketService.PrintAsync(_currentTicketSaleId.Value, isReprint);
            StatusMessage = isReprint
                ? "Ticket reimpresso."
                : "Ticket enviado para impressão.";
        }
        catch (DomainException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha na impressão: {ex.Message}";
        }
    }

    private async Task OpenReprintPanelAsync()
    {
        TodayTickets.Clear();
        foreach (var ticket in await _ticketService.GetTodayTicketsAsync())
            TodayTickets.Add(ticket);

        ShowTicketPanel = false;
        ShowCashMovementPanel = false;
        ShowCashClosePanel = false;
        ShowCashOpenPanel = false;
        PaymentPanelVisibility = "Collapsed";
        ShowReprintPanel = true;
        StatusMessage = TodayTickets.Count == 0
            ? "Nenhuma venda hoje para reimprimir."
            : $"{TodayTickets.Count} ticket(s) de hoje.";
    }

    private async Task ViewTicketAsync(TicketDto? ticket)
    {
        if (ticket is null)
            return;

        await ShowTicketAsync(ticket.SaleId, isReprint: true);
    }

    private async Task ReprintTicketAsync(TicketDto? ticket)
    {
        if (ticket is null)
            return;

        await ShowTicketAsync(ticket.SaleId, isReprint: true);
        await PrintCurrentTicketAsync();
    }
}
