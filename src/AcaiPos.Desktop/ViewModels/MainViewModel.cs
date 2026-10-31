using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Application.Sync;
using AcaiPos.Domain.Common;
using System.Windows.Media.Imaging;
using AcaiPos.Desktop.Printing;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Desktop.ViewModels;

/// <summary>
/// Parcela já lançada no pagamento da venda.
/// </summary>
public sealed class PaymentLineViewModel : ObservableObject
{
    public Guid FormaPagamentoId { get; init; }
    public string MethodLabel { get; init; } = string.Empty;
    public bool CalculaTroco { get; init; }
    public decimal Amount { get; init; }
    public decimal? AmountReceived { get; init; }
    public string AmountLabel => Amount.ToString("C");
}

/// <summary>
/// Botão de categoria na tela de venda.
/// </summary>
public sealed class CategoryChipViewModel : ObservableObject
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// Botão de forma de pagamento montado a partir da tabela FormasPagamento.
/// </summary>
public sealed class PaymentMethodButtonViewModel
{
    public const string DefaultButtonColor = "#2A3642";

    public Guid Id { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public bool CalculaTroco { get; init; }
    public bool IsParcela { get; init; }
    public Brush ButtonBrush { get; init; } = FrozenBrush(DefaultButtonColor);

    public static PaymentMethodButtonViewModel From(FormaPagamentoDto forma) => new()
    {
        Id = forma.Id,
        Descricao = forma.Descricao,
        CalculaTroco = forma.CalculaTroco,
        IsParcela = forma.IsParcela,
        ButtonBrush = FrozenBrush(forma.CorRgbBotao)
    };

    private static SolidColorBrush FrozenBrush(string? color)
    {
        var brush = new SolidColorBrush(ParseColor(color));
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string? color)
    {
        var text = color?.Trim();
        if (!string.IsNullOrEmpty(text)
            && text.Length == 7
            && text[0] == '#'
            && text[1..].All(Uri.IsHexDigit))
        {
            var converted = ColorConverter.ConvertFromString(text);
            if (converted is Color parsed)
                return parsed;
        }

        return (Color)ColorConverter.ConvertFromString(DefaultButtonColor)!;
    }
}

/// <summary>
/// Item do carrinho, com edição direta da quantidade.
/// </summary>
public sealed class CartLineViewModel : ObservableObject
{
    private decimal _quantity;
    private bool _isEditingQuantity;
    private string _quantityText = "1";

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
            RaisePropertyChanged(nameof(SubtotalLabel));
            RaisePropertyChanged(nameof(QuantityLabel));
        }
    }

    public bool IsEditingQuantity
    {
        get => _isEditingQuantity;
        set => SetProperty(ref _isEditingQuantity, value);
    }

    public string QuantityText
    {
        get => _quantityText;
        set => SetProperty(ref _quantityText, value);
    }

    public decimal Subtotal => Math.Round(Quantity * UnitPrice, 2);
    public string QuantityLabel => Quantity % 1 == 0 ? ((int)Quantity).ToString() : Quantity.ToString("0.###");
    public string UnitPriceLabel => UnitPrice.ToString("C");
    public string SubtotalLabel => Subtotal.ToString("C");

    /// <summary>
    /// Entra no modo de edição e copia a quantidade atual para o campo de texto.
    /// </summary>
    public void BeginQuantityEdit()
    {
        QuantityText = QuantityLabel;
        IsEditingQuantity = true;
    }
}

/// <summary>
/// Tela principal: login, venda, caixa, navegação e os painéis de pagamento e ticket.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly CatalogService _catalogService;
    private readonly IAvisoCatalogo _avisoCatalogo;
    private readonly CashRegisterService _cashRegisterService;
    private readonly SaleService _saleService;
    private readonly TicketService _ticketService;
    private readonly UserService _userService;
    private readonly NfceService _nfceService;
    private readonly ConfiguracaoService _configuracaoService;
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
    private int _productSearchGeneration;
    private bool _suppressSearchSchedule;
    private bool _suggestionsDismissed;
    private bool _suggestionsOpen;
    private int _suggestionIndex = -1;
    private bool _exibirBlocosProdutos = true;
    private bool _exibirCategorias = true;
    private Guid? _selectedCategoryId;
    private decimal _openingAmount = 100m;
    private decimal _countedAmount;
    private string _paymentPanelVisibility = "Collapsed";
    private string _paymentMethodPanelVisibility = "Collapsed";
    private bool _isEnteringPaymentAmount;
    private decimal _paymentEntryAmount;
    private decimal _paymentReceivedAmount;
    private string _paymentEntryMessage = string.Empty;
    private decimal _discountAmount;
    private decimal _surchargeAmount;
    private string _discountAuthorizerUsername = "gerente";
    private string _discountAuthorizerPin = string.Empty;
    private bool _discountAuthorized;
    private string _discountAuthMessage = "Informe usuário e PIN do gerente ou administrador.";
    private PaymentMethodButtonViewModel? _selectedPaymentMethod;
    private string _lastTicketInfo = string.Empty;
    private bool _showCashOpenPanel;
    private bool _showCashClosePanel;
    private bool _showCashMovementPanel;
    private bool _showTicketPanel;
    private bool _showReprintPanel;
    private bool _showCashMenu;
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
    private bool _isNavCollapsed;
    private bool _emitirCupomFiscal;
    private string _cpfConsumidor = string.Empty;
    private bool _nfceEnvioEmAndamento;
    private string _nfceEnvioMensagem = "Comunicando com a SEFAZ";
    private BitmapImage? _ticketQrImage;

    /// <summary>
    /// Monta as telas filhas, os comandos e o relógio da barra de status.
    /// </summary>
    public MainViewModel(
        AuthService authService,
        CatalogService catalogService,
        CashRegisterService cashRegisterService,
        SaleService saleService,
        TicketService ticketService,
        UserService userService,
        NfceService nfceService,
        ConfiguracaoService configuracaoService,
        InscricaoTerminalService inscricaoService,
        ICurrentSession session,
        ICashMovementPrinter cashMovementPrinter,
        IAvisoCatalogo avisoCatalogo)
    {
        _authService = authService;
        _catalogService = catalogService;
        _cashRegisterService = cashRegisterService;
        _saleService = saleService;
        _ticketService = ticketService;
        _userService = userService;
        _nfceService = nfceService;
        _configuracaoService = configuracaoService;
        _session = session;
        _avisoCatalogo = avisoCatalogo;
        _avisoCatalogo.Aplicado += AoCatalogoAplicado;

        Catalog = new CatalogManagementViewModel(
            catalogService,
            inscricaoService,
            msg => StatusMessage = msg,
            async () => await LoadCatalogAsync());

        Users = new UserManagementViewModel(
            userService,
            msg => StatusMessage = msg);

        Fiscal = new FiscalConfigViewModel(
            nfceService,
            msg => StatusMessage = msg);

        Configuracoes = new ConfiguracoesViewModel(
            configuracaoService,
            inscricaoService,
            msg => StatusMessage = msg,
            (blocos, categorias) => AplicarExibicaoVenda(blocos, categorias));

        History = new SalesHistoryViewModel(
            saleService,
            nfceService,
            cashRegisterService,
            msg => StatusMessage = msg,
            async id => await ShowTicketAsync(id, isReprint: true),
            async id =>
            {
                await ShowTicketAsync(id, isReprint: true);
                await PrintCurrentTicketAsync();
            });

        NfceGestao = new NfceGestaoViewModel(
            nfceService,
            saleService,
            session,
            msg => StatusMessage = msg);

        CashSession = new CashSessionViewModel(
            cashRegisterService,
            cashMovementPrinter,
            session,
            msg => StatusMessage = msg);

        LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => !IsAuthenticated);
        LogoutCommand = new RelayCommand(async _ => await LogoutAsync(), _ => IsAuthenticated);
        OpenCashCommand = new RelayCommand(_ =>
        {
            ShowCashMenu = false;
            ShowCashOpenPanel = true;
        }, _ => IsAuthenticated && !IsCashOpen);
        ConfirmOpenCashCommand = new RelayCommand(async _ => await OpenCashAsync(), _ => IsAuthenticated && !IsCashOpen);
        CancelOpenCashCommand = new RelayCommand(_ => ShowCashOpenPanel = false);
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
        BeginEditQuantityCommand = new RelayCommand(p => BeginEditQuantity(p as CartLineViewModel), _ => IsCashOpen);
        CommitQuantityCommand = new RelayCommand(p => CommitQuantity(p as CartLineViewModel));
        CancelQuantityEditCommand = new RelayCommand(p => CancelQuantityEdit(p as CartLineViewModel));
        ClearCartCommand = new RelayCommand(_ => ClearCart(), _ => Cart.Count > 0);
        SearchCommand = new RelayCommand(async _ => await ReloadProductsNowAsync());
        SearchOrBarcodeCommand = new RelayCommand(async _ => await SearchOrBarcodeAsync());
        MoveSuggestionCommand = new RelayCommand(MoveSuggestion);
        DismissSuggestionsCommand = new RelayCommand(_ => DismissSuggestions());
        AcceptSuggestionCommand = new RelayCommand(async p => await AcceptSuggestionAsync(p as ProductDto), _ => IsCashOpen);
        StartPaymentCommand = new RelayCommand(_ => StartPayment(), _ => IsCashOpen && Cart.Count > 0);
        CancelPaymentCommand = new RelayCommand(_ => CancelPaymentPanel());
        AuthorizeDiscountCommand = new RelayCommand(_ => AuthorizeDiscount());
        OpenPaymentMethodCommand = new RelayCommand(_ => OpenPaymentMethodPanel(), _ => CanOpenPaymentMethod());
        SelectPaymentMethodCommand = new RelayCommand(p => SelectPaymentMethod(p));
        AcceptPaymentAmountCommand = new RelayCommand(_ => AcceptPaymentAmount());
        ClosePaymentMethodCommand = new RelayCommand(_ => ClosePaymentMethodPanel());
        BackToPaymentMethodsCommand = new RelayCommand(_ => BackToPaymentMethods());
        RemovePaymentLineCommand = new RelayCommand(p => RemovePaymentLine(p as PaymentLineViewModel));
        SavePaymentCommand = new RelayCommand(async _ => await CompleteSaleAsync(PaymentLines.Select(ToDraft).ToList()), _ => CanSavePayment());
        AppendPinCommand = new RelayCommand(p => AppendPin(p?.ToString()));
        ClearPinCommand = new RelayCommand(_ => Pin = string.Empty);
        BackspacePinCommand = new RelayCommand(_ =>
        {
            if (Pin.Length > 0)
                Pin = Pin[..^1];
        });
        GoToSaleCommand = new RelayCommand(async _ => await NavigateToSaleAsync(), _ => IsAuthenticated);
        GoToCatalogCommand = new RelayCommand(async _ => await NavigateToCatalogAsync(), _ => IsAuthenticated && CanManageCatalog);
        GoToFiscalCommand = new RelayCommand(async _ => await NavigateToFiscalAsync(), _ => IsAuthenticated && CanManageCatalog);
        GoToSettingsCommand = new RelayCommand(async _ => await NavigateToSettingsAsync(), _ => IsAuthenticated && CanManageCatalog);
        ToggleCupomFiscalCommand = new RelayCommand(_ => EmitirCupomFiscal = !EmitirCupomFiscal);
        GoToUsersCommand = new RelayCommand(async _ => await NavigateToUsersAsync(), _ => IsAuthenticated && CanManageUsers);
        GoToHistoryCommand = new RelayCommand(async _ => await NavigateToHistoryAsync(), _ => IsAuthenticated);
        GoToNfceCommand = new RelayCommand(async _ => await NavigateToNfceAsync(), _ => IsAuthenticated);
        GoToCashSessionCommand = new RelayCommand(async _ => await NavigateToCashSessionAsync(), _ => IsAuthenticated);
        OpenSupplyCommand = new RelayCommand(async _ => await OpenMovementPanelAsync(CashMovementType.Supply), _ => IsCashOpen);
        OpenWithdrawalCommand = new RelayCommand(async _ => await OpenMovementPanelAsync(CashMovementType.Withdrawal), _ => IsCashOpen);
        CancelCashMovementCommand = new RelayCommand(_ => ShowCashMovementPanel = false);
        ConfirmCashMovementCommand = new RelayCommand(async _ => await ConfirmCashMovementAsync(), _ => IsCashOpen);
        CloseTicketCommand = new RelayCommand(_ => CloseTicketPanel());
        PrintTicketCommand = new RelayCommand(async _ => await PrintCurrentTicketAsync(), _ => _currentTicketSaleId.HasValue);
        OpenReprintCommand = new RelayCommand(async _ => await OpenReprintPanelAsync(), _ => IsAuthenticated);
        ToggleCashMenuCommand = new RelayCommand(_ => ShowCashMenu = !ShowCashMenu, _ => IsAuthenticated);
        ToggleNavCommand = new RelayCommand(_ => IsNavCollapsed = !IsNavCollapsed);
        CloseReprintCommand = new RelayCommand(_ => ShowReprintPanel = false);
        ViewTicketCommand = new RelayCommand(async p => await ViewTicketAsync(p as TicketDto), _ => IsAuthenticated);
        ReprintSelectedCommand = new RelayCommand(async p => await ReprintTicketAsync(p as TicketDto), _ => IsAuthenticated);

        PaymentLines.CollectionChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(PaymentLinesEmptyVisibility));
            RaisePaymentFacts();
        };

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (_, _) => ClockText = DateTime.Now.ToString("HH:mm:ss");
        timer.Start();
    }

    public CatalogManagementViewModel Catalog { get; }
    public FiscalConfigViewModel Fiscal { get; }
    public ConfiguracoesViewModel Configuracoes { get; }
    public UserManagementViewModel Users { get; }
    public SalesHistoryViewModel History { get; }
    public NfceGestaoViewModel NfceGestao { get; }
    public CashSessionViewModel CashSession { get; }

    public ObservableCollection<CategoryChipViewModel> Categories { get; } = [];
    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<CartLineViewModel> Cart { get; } = [];
    public ObservableCollection<CashMovementDto> CashMovements { get; } = [];
    public ObservableCollection<TicketDto> TodayTickets { get; } = [];
    public ObservableCollection<PaymentLineViewModel> PaymentLines { get; } = [];
    public ObservableCollection<PaymentMethodButtonViewModel> PaymentMethods { get; } = [];

    public RelayCommand LoginCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand OpenCashCommand { get; }
    public RelayCommand ConfirmOpenCashCommand { get; }
    public RelayCommand CancelOpenCashCommand { get; }
    public RelayCommand CloseCashCommand { get; }
    public RelayCommand ConfirmCloseCashCommand { get; }
    public RelayCommand CancelCloseCashCommand { get; }
    public RelayCommand SelectCategoryCommand { get; }
    public RelayCommand AddProductCommand { get; }
    public RelayCommand IncreaseItemCommand { get; }
    public RelayCommand DecreaseItemCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand BeginEditQuantityCommand { get; }
    public RelayCommand CommitQuantityCommand { get; }
    public RelayCommand CancelQuantityEditCommand { get; }
    public RelayCommand ClearCartCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand SearchOrBarcodeCommand { get; }
    public RelayCommand MoveSuggestionCommand { get; }
    public RelayCommand DismissSuggestionsCommand { get; }
    public RelayCommand AcceptSuggestionCommand { get; }
    public RelayCommand StartPaymentCommand { get; }
    public RelayCommand CancelPaymentCommand { get; }
    public RelayCommand AuthorizeDiscountCommand { get; }
    public RelayCommand OpenPaymentMethodCommand { get; }
    public RelayCommand SelectPaymentMethodCommand { get; }
    public RelayCommand AcceptPaymentAmountCommand { get; }
    public RelayCommand ClosePaymentMethodCommand { get; }
    public RelayCommand BackToPaymentMethodsCommand { get; }
    public RelayCommand RemovePaymentLineCommand { get; }
    public RelayCommand SavePaymentCommand { get; }
    public RelayCommand AppendPinCommand { get; }
    public RelayCommand ClearPinCommand { get; }
    public RelayCommand BackspacePinCommand { get; }
    public RelayCommand GoToSaleCommand { get; }
    public RelayCommand GoToCatalogCommand { get; }
    public RelayCommand GoToFiscalCommand { get; }
    public RelayCommand GoToSettingsCommand { get; }
    public RelayCommand ToggleCupomFiscalCommand { get; }
    public RelayCommand GoToUsersCommand { get; }
    public RelayCommand GoToHistoryCommand { get; }
    public RelayCommand GoToNfceCommand { get; }
    public RelayCommand GoToCashSessionCommand { get; }
    public RelayCommand OpenSupplyCommand { get; }
    public RelayCommand OpenWithdrawalCommand { get; }
    public RelayCommand CancelCashMovementCommand { get; }
    public RelayCommand ConfirmCashMovementCommand { get; }
    public RelayCommand CloseTicketCommand { get; }
    public RelayCommand PrintTicketCommand { get; }
    public RelayCommand OpenReprintCommand { get; }
    public RelayCommand ToggleCashMenuCommand { get; }
    public RelayCommand ToggleNavCommand { get; }
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
            ConfirmOpenCashCommand.RaiseCanExecuteChanged();
            CloseCashCommand.RaiseCanExecuteChanged();
            GoToSaleCommand.RaiseCanExecuteChanged();
            GoToCatalogCommand.RaiseCanExecuteChanged();
            GoToFiscalCommand.RaiseCanExecuteChanged();
            GoToSettingsCommand.RaiseCanExecuteChanged();
            GoToUsersCommand.RaiseCanExecuteChanged();
            GoToHistoryCommand.RaiseCanExecuteChanged();
            GoToNfceCommand.RaiseCanExecuteChanged();
            GoToCashSessionCommand.RaiseCanExecuteChanged();
            OpenReprintCommand.RaiseCanExecuteChanged();
            ToggleCashMenuCommand.RaiseCanExecuteChanged();
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
            ConfirmOpenCashCommand.RaiseCanExecuteChanged();
            CloseCashCommand.RaiseCanExecuteChanged();
            ConfirmCloseCashCommand.RaiseCanExecuteChanged();
            OpenSupplyCommand.RaiseCanExecuteChanged();
            OpenWithdrawalCommand.RaiseCanExecuteChanged();
            ConfirmCashMovementCommand.RaiseCanExecuteChanged();
            AddProductCommand.RaiseCanExecuteChanged();
            AcceptSuggestionCommand.RaiseCanExecuteChanged();
            BeginEditQuantityCommand.RaiseCanExecuteChanged();
            StartPaymentCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CashMovementButtonsVisibility));
        }
    }

    public string LoginVisibility => IsAuthenticated ? "Collapsed" : "Visible";
    public string WorkspaceVisibility => IsAuthenticated ? "Visible" : "Collapsed";
    public string CashStatusText => IsCashOpen ? "CAIXA ABERTO" : "CAIXA FECHADO";
    public string SaleScreenVisibility => CurrentScreen == "Sale" ? "Visible" : "Collapsed";
    public string ProductBlocksVisibility => _exibirBlocosProdutos ? "Visible" : "Collapsed";
    public string CategoryCarouselVisibility => _exibirCategorias ? "Visible" : "Collapsed";
    public string CatalogScreenVisibility => CurrentScreen == "Catalog" ? "Visible" : "Collapsed";
    public string FiscalScreenVisibility => CurrentScreen == "Fiscal" ? "Visible" : "Collapsed";
    public string SettingsScreenVisibility => CurrentScreen == "Settings" ? "Visible" : "Collapsed";
    public string UsersScreenVisibility => CurrentScreen == "Users" ? "Visible" : "Collapsed";
    public string HistoryScreenVisibility => CurrentScreen == "History" ? "Visible" : "Collapsed";
    public string NfceScreenVisibility => CurrentScreen == "Nfce" ? "Visible" : "Collapsed";
    public string CashSessionScreenVisibility => CurrentScreen == "CashSession" ? "Visible" : "Collapsed";
    public bool IsSaleScreen => CurrentScreen == "Sale";
    public bool IsHistoryScreen => CurrentScreen == "History";
    public bool IsNfceScreen => CurrentScreen == "Nfce";
    public bool IsCashSessionScreen => CurrentScreen == "CashSession";
    public bool IsCatalogScreen => CurrentScreen == "Catalog";
    public bool IsFiscalScreen => CurrentScreen == "Fiscal";
    public bool IsSettingsScreen => CurrentScreen == "Settings";
    public bool IsUsersScreen => CurrentScreen == "Users";

    public string CurrentScreen
    {
        get => _currentScreen;
        set
        {
        ShowCashMenu = false;
        if (!string.Equals(value, "CashSession", StringComparison.Ordinal))
            CashSession.ClosePreview();
        if (!string.Equals(value, "Nfce", StringComparison.Ordinal))
            NfceGestao.ClosePreview();
        SetProperty(ref _currentScreen, value);
            RaisePropertyChanged(nameof(SaleScreenVisibility));
            RaisePropertyChanged(nameof(CatalogScreenVisibility));
            RaisePropertyChanged(nameof(FiscalScreenVisibility));
            RaisePropertyChanged(nameof(SettingsScreenVisibility));
            RaisePropertyChanged(nameof(UsersScreenVisibility));
            RaisePropertyChanged(nameof(HistoryScreenVisibility));
            RaisePropertyChanged(nameof(NfceScreenVisibility));
            RaisePropertyChanged(nameof(CashSessionScreenVisibility));
            RaisePropertyChanged(nameof(IsSaleScreen));
            RaisePropertyChanged(nameof(IsHistoryScreen));
            RaisePropertyChanged(nameof(IsNfceScreen));
            RaisePropertyChanged(nameof(IsCashSessionScreen));
            RaisePropertyChanged(nameof(IsCatalogScreen));
            RaisePropertyChanged(nameof(IsFiscalScreen));
            RaisePropertyChanged(nameof(IsSettingsScreen));
            RaisePropertyChanged(nameof(IsUsersScreen));
        }
    }

    public bool CanManageCatalog
    {
        get => _canManageCatalog;
        set
        {
            SetProperty(ref _canManageCatalog, value);
            GoToCatalogCommand.RaiseCanExecuteChanged();
            GoToFiscalCommand.RaiseCanExecuteChanged();
            GoToSettingsCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CatalogButtonVisibility));
            RaisePropertyChanged(nameof(AdminSectionVisibility));
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
            RaisePropertyChanged(nameof(AdminSectionVisibility));
        }
    }

    public bool EmitirCupomFiscal
    {
        get => _emitirCupomFiscal;
        set
        {
            SetProperty(ref _emitirCupomFiscal, value);
            RaisePropertyChanged(nameof(CupomFiscalLabel));
            RaisePropertyChanged(nameof(CpfConsumidorVisibility));
        }
    }

    public string CpfConsumidor
    {
        get => _cpfConsumidor;
        set => SetProperty(ref _cpfConsumidor, value);
    }

    public string CupomFiscalLabel => EmitirCupomFiscal ? "Cupom fiscal: LIGADO" : "Cupom fiscal: DESLIGADO";
    public string CpfConsumidorVisibility => EmitirCupomFiscal ? "Visible" : "Collapsed";
    public string NfceEnvioVisivel => _nfceEnvioEmAndamento ? "Visible" : "Collapsed";

    public string NfceEnvioMensagem
    {
        get => _nfceEnvioMensagem;
        private set => SetProperty(ref _nfceEnvioMensagem, value);
    }
    public BitmapImage? TicketQrImage
    {
        get => _ticketQrImage;
        private set
        {
            SetProperty(ref _ticketQrImage, value);
            RaisePropertyChanged(nameof(TicketQrVisibility));
        }
    }

    public string TicketQrVisibility => TicketQrImage is null ? "Collapsed" : "Visible";

    public string CatalogButtonVisibility => CanManageCatalog ? "Visible" : "Collapsed";
    public string UsersButtonVisibility => CanManageUsers ? "Visible" : "Collapsed";
    public string AdminSectionVisibility => CanManageCatalog || CanManageUsers ? "Visible" : "Collapsed";

    public bool IsNavCollapsed
    {
        get => _isNavCollapsed;
        set
        {
            SetProperty(ref _isNavCollapsed, value);
            RaisePropertyChanged(nameof(NavLabelVisibility));
            RaisePropertyChanged(nameof(NavRailWidth));
            RaisePropertyChanged(nameof(NavToggleGlyph));
            RaisePropertyChanged(nameof(NavToggleHorizontalAlignment));
            RaisePropertyChanged(nameof(NavToggleToolTip));
        }
    }

    public string NavLabelVisibility => IsNavCollapsed ? "Collapsed" : "Visible";
    public double NavRailWidth => IsNavCollapsed ? 84 : 240;
    public string NavToggleGlyph => IsNavCollapsed ? "\uE76C" : "\uE76B";
    public string NavToggleHorizontalAlignment => IsNavCollapsed ? "Center" : "Right";
    public string NavToggleToolTip => IsNavCollapsed ? "Expandir menu" : "Recolher menu";

    public bool ShowCashMenu
    {
        get => _showCashMenu;
        set => SetProperty(ref _showCashMenu, value);
    }
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
        set
        {
            if (_searchText == value)
                return;

            _searchText = value;
            RaisePropertyChanged();
            _suggestionsDismissed = false;
            SuggestionIndex = -1;
            if (!_suppressSearchSchedule)
                ScheduleProductSearch();

            if (_suppressSearchSchedule || value.Trim().Length < 3)
                SyncSuggestionsOpen();
        }
    }

    /// <summary>
    /// Lista de sugestões visível. Abre a partir do terceiro caractere, sem destacar item.
    /// </summary>
    public bool SuggestionsOpen => _suggestionsOpen;

    /// <summary>
    /// Índice do item destacado na lista. -1 significa que Enter não lança sugestão.
    /// </summary>
    public int SuggestionIndex
    {
        get => _suggestionIndex;
        private set => SetProperty(ref _suggestionIndex, value);
    }

    public string SuggestionsListVisibility =>
        _suggestionsOpen && Products.Count > 0 ? "Visible" : "Collapsed";

    public string SuggestionsEmptyVisibility =>
        _suggestionsOpen && Products.Count == 0 ? "Visible" : "Collapsed";

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

    public string PaymentMethodPanelVisibility
    {
        get => _paymentMethodPanelVisibility;
        set
        {
            SetProperty(ref _paymentMethodPanelVisibility, value);
            RaisePropertyChanged(nameof(PaymentMethodListVisibility));
            RaisePropertyChanged(nameof(PaymentAmountEntryVisibility));
        }
    }

    public string PaymentMethodListVisibility =>
        _paymentMethodPanelVisibility == "Visible" && !_isEnteringPaymentAmount ? "Visible" : "Collapsed";

    public string PaymentAmountEntryVisibility =>
        _paymentMethodPanelVisibility == "Visible" && _isEnteringPaymentAmount ? "Visible" : "Collapsed";

    public string PaymentLinesEmptyVisibility => PaymentLines.Count == 0 ? "Visible" : "Collapsed";

    public string SelectedPaymentMethodLabel => SelectedPaymentMethod?.Descricao ?? string.Empty;

    public string AsksReceivedAmountVisibility =>
        SelectedPaymentMethod?.CalculaTroco == true ? "Visible" : "Collapsed";

    public string PaymentChangeLabel =>
        (SelectedPaymentMethod?.CalculaTroco == true
            ? Math.Max(0, decimal.Round(PaymentReceivedAmount - PaymentEntryAmount, 2))
            : 0m).ToString("C");

    public decimal PaymentEntryAmount
    {
        get => _paymentEntryAmount;
        set
        {
            var rounded = decimal.Round(Math.Max(0, value), 2);
            if (_paymentEntryAmount == rounded)
                return;

            _paymentEntryAmount = rounded;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(PaymentChangeLabel));
            DeferMoneyTextRefresh(nameof(PaymentEntryAmountText));
        }
    }

    public string PaymentEntryAmountText
    {
        get => _paymentEntryAmount.ToString("N2");
        set => ApplyMoneyText(value, amount => PaymentEntryAmount = amount, nameof(PaymentEntryAmountText));
    }

    public decimal PaymentReceivedAmount
    {
        get => _paymentReceivedAmount;
        set
        {
            var rounded = decimal.Round(Math.Max(0, value), 2);
            if (_paymentReceivedAmount == rounded)
                return;

            _paymentReceivedAmount = rounded;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(PaymentChangeLabel));
            DeferMoneyTextRefresh(nameof(PaymentReceivedAmountText));
        }
    }

    public string PaymentReceivedAmountText
    {
        get => _paymentReceivedAmount.ToString("N2");
        set => ApplyMoneyText(value, amount => PaymentReceivedAmount = amount, nameof(PaymentReceivedAmountText));
    }

    public string PaymentEntryMessage
    {
        get => _paymentEntryMessage;
        private set
        {
            SetProperty(ref _paymentEntryMessage, value);
            RaisePropertyChanged(nameof(PaymentEntryMessageVisibility));
        }
    }

    public string PaymentEntryMessageVisibility =>
        string.IsNullOrEmpty(_paymentEntryMessage) ? "Collapsed" : "Visible";

    public decimal DiscountAmount
    {
        get => _discountAmount;
        set
        {
            var capped = Math.Max(0, Math.Min(value, CartTotal));
            var rounded = decimal.Round(capped, 2);
            var changed = _discountAmount != rounded;
            SetProperty(ref _discountAmount, rounded);
            if (changed)
            {
                InvalidateDiscountAuthorization();
                NotifyAllocationDrift();
            }
            RaisePaymentFacts();
            RaisePropertyChanged(nameof(DiscountAuthVisibility));
            DeferMoneyTextRefresh(nameof(DiscountAmountText));
        }
    }

    public string DiscountAmountText
    {
        get => _discountAmount.ToString("N2");
        set => ApplyMoneyText(value, amount => DiscountAmount = amount, nameof(DiscountAmountText));
    }

    public decimal SurchargeAmount
    {
        get => _surchargeAmount;
        set
        {
            var rounded = decimal.Round(Math.Max(0, value), 2);
            var changed = _surchargeAmount != rounded;
            SetProperty(ref _surchargeAmount, rounded);
            if (changed)
                NotifyAllocationDrift();
            RaisePaymentFacts();
            DeferMoneyTextRefresh(nameof(SurchargeAmountText));
        }
    }

    public string SurchargeAmountText
    {
        get => _surchargeAmount.ToString("N2");
        set => ApplyMoneyText(value, amount => SurchargeAmount = amount, nameof(SurchargeAmountText));
    }

    /// <summary>
    /// Interpreta um valor monetário no formato local ou invariante.
    /// </summary>
    private static bool TryParseMoney(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Aplica o texto digitado ao valor e reagenda a formatação do campo.
    /// </summary>
    private void ApplyMoneyText(string? text, Action<decimal> assign, string textPropertyName)
    {
        if (TryParseMoney(text, out var parsed))
            assign(decimal.Round(parsed, 2));

        DeferMoneyTextRefresh(textPropertyName);
    }

    /// <summary>
    /// Reaplica a máscara do campo depois do ciclo de binding, para não brigar com a digitação.
    /// </summary>
    private void DeferMoneyTextRefresh(string propertyName)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess() == false)
        {
            RaisePropertyChanged(propertyName);
            return;
        }

        dispatcher.BeginInvoke(() => RaisePropertyChanged(propertyName));
    }

    public string DiscountAuthorizerUsername
    {
        get => _discountAuthorizerUsername;
        set
        {
            if (_discountAuthorizerUsername == value)
                return;

            SetProperty(ref _discountAuthorizerUsername, value);
            InvalidateDiscountAuthorization();
        }
    }

    public string DiscountAuthorizerPin
    {
        get => _discountAuthorizerPin;
        set
        {
            if (_discountAuthorizerPin == value)
                return;

            SetProperty(ref _discountAuthorizerPin, value);
            InvalidateDiscountAuthorization();
        }
    }

    public bool DiscountAuthorized
    {
        get => _discountAuthorized;
        private set
        {
            if (_discountAuthorized == value)
                return;

            _discountAuthorized = value;
            RaisePropertyChanged(nameof(DiscountAuthorized));
            RaisePaymentCommandStates();
        }
    }

    public string DiscountAuthMessage
    {
        get => _discountAuthMessage;
        private set => SetProperty(ref _discountAuthMessage, value);
    }

    public PaymentMethodButtonViewModel? SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        private set
        {
            SetProperty(ref _selectedPaymentMethod, value);
            RaisePropertyChanged(nameof(SelectedPaymentMethodLabel));
            RaisePropertyChanged(nameof(AsksReceivedAmountVisibility));
            RaisePropertyChanged(nameof(PaymentChangeLabel));
        }
    }

    public decimal PayableTotal => Math.Max(0, decimal.Round(CartTotal - DiscountAmount + SurchargeAmount, 2));
    public decimal PaymentPaid => PaymentLines.Sum(p => p.Amount);
    public decimal PaymentRemaining => Math.Max(0, PayableTotal - PaymentPaid);
    public string PayableTotalLabel => PayableTotal.ToString("C");
    public string PaymentPaidLabel => PaymentPaid.ToString("C");
    public string PaymentRemainingLabel => PaymentRemaining.ToString("C");
    public string PaymentBalanceLabel
    {
        get
        {
            var balance = decimal.Round(PayableTotal - PaymentPaid, 2);
            return balance < -0.009m
                ? $"Excedente: {Math.Abs(balance):C}"
                : $"Restante: {balance:C}";
        }
    }
    public string CartTotalLabel => CartTotal.ToString("C");
    public string CartCountLabel => $"{Cart.Sum(i => i.Quantity):0.###} itens";
    public decimal CartTotal => Cart.Sum(i => i.Subtotal);

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

    /// <summary>
    /// Autentica o operador, libera as telas conforme o papel e abre o catálogo de venda.
    /// </summary>
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
                : "Login ok. Caixa fechado. A abertura é opcional; vendas exigem caixa aberto.";
            ShowCashOpenPanel = false;
            await LoadCatalogAsync();
            await TransmitirContingenciasAsync();
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

    /// <summary>
    /// Encerra a sessão e limpa carrinho, painéis e permissões.
    /// </summary>
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
        SurchargeAmount = 0;
        Categories.Clear();
        Products.Clear();
        OperatorName = string.Empty;
        TerminalLabel = string.Empty;
        StatusMessage = "Sessão encerrada.";
        ClosePaymentMethodPanel();
        PaymentPanelVisibility = "Collapsed";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        ShowTicketPanel = false;
        ShowReprintPanel = false;
        ShowCashMenu = false;
        TodayTickets.Clear();
        CashMovements.Clear();
        RefreshCartTotals();
    }

    /// <summary>
    /// Volta para a tela de venda e recarrega o catálogo.
    /// </summary>
    private async Task NavigateToSaleAsync()
    {
        CurrentScreen = "Sale";
        await LoadCatalogAsync();
        StatusMessage = "Tela de venda.";
    }

    /// <summary>
    /// Abre o cadastro de produtos e categorias, se o papel permitir.
    /// </summary>
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

    private async Task NavigateToFiscalAsync()
    {
        if (!CanManageCatalog)
        {
            StatusMessage = "Sem permissão para a configuração fiscal.";
            return;
        }

        CurrentScreen = "Fiscal";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        PaymentPanelVisibility = "Collapsed";
        await Fiscal.LoadAsync();
        StatusMessage = "Configuração fiscal.";
    }

    private async Task NavigateToSettingsAsync()
    {
        if (!CanManageCatalog)
        {
            StatusMessage = "Sem permissão para as configurações.";
            return;
        }

        CurrentScreen = "Settings";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        PaymentPanelVisibility = "Collapsed";
        await Configuracoes.LoadAsync();
        StatusMessage = "Configurações.";
    }

    private async Task TransmitirContingenciasAsync()
    {
        try
        {
            var resultado = await _nfceService.TransmitirPendentesAsync();
            if (resultado.Autorizadas > 0 || resultado.Rejeitadas > 0 || resultado.Pendentes > 0)
                StatusMessage = resultado.Mensagem;
        }
        catch (DomainException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"Contingências não transmitidas: {ex.Message}";
        }
    }

    /// <summary>
    /// Abre o cadastro de usuários, se o papel for administrador.
    /// </summary>
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

    /// <summary>
    /// Abre o histórico e carrega as vendas do ciclo atual.
    /// </summary>
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

    /// <summary>
    /// Abre a gestão de NFC-e no caixa em operação.
    /// </summary>
    private async Task NavigateToNfceAsync()
    {
        CurrentScreen = "Nfce";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        ShowTicketPanel = false;
        ShowReprintPanel = false;
        PaymentPanelVisibility = "Collapsed";
        await NfceGestao.InitializeAsync();
        StatusMessage = NfceGestao.Message;
    }

    /// <summary>
    /// Abre os ciclos de caixa no mês corrente.
    /// </summary>
    private async Task NavigateToCashSessionAsync()
    {
        CurrentScreen = "CashSession";
        ShowCashOpenPanel = false;
        ShowCashClosePanel = false;
        ShowCashMovementPanel = false;
        ShowTicketPanel = false;
        ShowReprintPanel = false;
        PaymentPanelVisibility = "Collapsed";
        await CashSession.LoadAsync(resetToCurrentMonth: true);
        StatusMessage = CashSession.Message;
    }

    /// <summary>
    /// Atualiza os ciclos se essa tela estiver visível.
    /// </summary>
    private async Task RefreshCashSessionIfVisibleAsync()
    {
        if (CurrentScreen == "CashSession")
            await CashSession.LoadAsync();
    }

    /// <summary>
    /// Abre o painel de sangria ou suprimento do caixa aberto.
    /// </summary>
    private async Task OpenMovementPanelAsync(CashMovementType type)
    {
        ShowCashMenu = false;
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

    /// <summary>
    /// Registra a movimentação e atualiza o resumo do caixa aberto.
    /// </summary>
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
            await RefreshCashSessionIfVisibleAsync();
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

    /// <summary>
    /// Recarrega as movimentações e o texto de saldo esperado do caixa aberto.
    /// </summary>
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

    /// <summary>
    /// Abre o caixa com o fundo informado.
    /// </summary>
    private async Task OpenCashAsync()
    {
        try
        {
            await _cashRegisterService.OpenAsync(OpeningAmount);
            IsCashOpen = true;
            ShowCashOpenPanel = false;
            StatusMessage = $"Caixa aberto com fundo de {OpeningAmount:C}.";
            await RefreshCashSessionIfVisibleAsync();
        }
        catch (DomainException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// Abre o painel de fechamento com o valor esperado já preenchido.
    /// </summary>
    private async void BeginCloseCash()
    {
        ShowCashMenu = false;
        try
        {
        var summary = await _cashRegisterService.GetOpenSummaryAsync();
        if (summary is null)
            return;

        CountedAmount = summary.ExpectedCash;
        CashSummaryText =
            $"Abertura {summary.OpeningAmount:C} · Vendas com troco {summary.CashSales:C} · " +
            $"Suprimentos {summary.Supplies:C} · Sangrias {summary.Withdrawals:C} · Esperado {summary.ExpectedCash:C}";
        ShowCashMovementPanel = false;
        ShowCashClosePanel = true;
        StatusMessage = $"Esperado em dinheiro: {summary.ExpectedCash:C}";
        }
        catch (DomainException ex)
        {
            System.Diagnostics. Debug.WriteLine(ex.Message);
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// Fecha o caixa com o valor contado, limpa o carrinho e informa a diferença.
    /// </summary>
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
            await RefreshCashSessionIfVisibleAsync();
        }
        catch (DomainException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// Carrega categorias ativas. Com o carrossel ligado e uma categoria padrão, ela já vem selecionada.
    /// </summary>
    private async Task LoadCatalogAsync()
    {
        await LoadSaleLayoutAsync();
        Categories.Clear();
        Categories.Add(new CategoryChipViewModel { Id = Guid.Empty, Name = "Todos" });
        var categories = await _catalogService.GetActiveCategoriesAsync();
        foreach (var category in categories)
            Categories.Add(new CategoryChipViewModel { Id = category.Id, Name = category.Name });

        var padrao = categories.FirstOrDefault(category => category.IsDefault);
        _selectedCategoryId = _exibirCategorias && padrao is not null ? padrao.Id : null;
        MarcarCategoriaSelecionada();
        await LoadProductsAsync();
        await LoadPaymentMethodsAsync();
    }

    private void AoCatalogoAplicado()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            _ = AtualizarCatalogoNaTelaAsync();
            return;
        }

        dispatcher.InvokeAsync(AtualizarCatalogoNaTelaAsync);
    }

    private async Task AtualizarCatalogoNaTelaAsync()
    {
        _catalogService.RecarregarAcompanhados();
        if (!IsAuthenticated)
            return;

        await AtualizarGradeVendaAsync();
        if (CanManageCatalog)
            await Catalog.AtualizarListasAsync();
    }

    private async Task AtualizarGradeVendaAsync()
    {
        var categorias = await _catalogService.GetActiveCategoriesAsync();
        var selecionada = _selectedCategoryId;
        Categories.Clear();
        Categories.Add(new CategoryChipViewModel { Id = Guid.Empty, Name = "Todos" });
        foreach (var category in categorias)
            Categories.Add(new CategoryChipViewModel { Id = category.Id, Name = category.Name });

        if (selecionada is Guid id && categorias.All(category => category.Id != id))
            _selectedCategoryId = null;

        MarcarCategoriaSelecionada();
        await LoadProductsAsync();
        await LoadPaymentMethodsAsync();
    }

    /// <summary>
    /// Lê se a venda mostra os blocos de produto e o carrossel de categorias.
    /// </summary>
    private async Task LoadSaleLayoutAsync()
    {
        var exibicao = await _configuracaoService.ObterExibicaoVendaAsync();
        AplicarExibicaoVenda(exibicao.BlocosProdutos, exibicao.Categorias, recarregarProdutos: false);
    }

    /// <summary>
    /// Aplica a exibição da venda. Sem categorias, a busca deixa de filtrar pela categoria padrão.
    /// </summary>
    private void AplicarExibicaoVenda(bool blocosProdutos, bool categorias, bool recarregarProdutos = true)
    {
        var mudou = _exibirBlocosProdutos != blocosProdutos || _exibirCategorias != categorias;
        _exibirBlocosProdutos = blocosProdutos;
        _exibirCategorias = categorias;
        if (!categorias)
            _selectedCategoryId = null;

        if (mudou)
        {
            RaisePropertyChanged(nameof(ProductBlocksVisibility));
            RaisePropertyChanged(nameof(CategoryCarouselVisibility));
        }

        if (recarregarProdutos)
            _ = ReloadProductsNowAsync();
    }

    /// <summary>
    /// Carrega as formas de pagamento habilitadas, na ordem da tabela.
    /// </summary>
    private async Task LoadPaymentMethodsAsync()
    {
        var formas = await _saleService.ListPaymentMethodsAsync(enabledOnly: true);
        PaymentMethods.Clear();
        foreach (var forma in formas)
            PaymentMethods.Add(PaymentMethodButtonViewModel.From(forma));
    }

    /// <summary>
    /// Filtra os produtos pela categoria. Guid vazio volta para todos.
    /// </summary>
    private async Task SelectCategoryAsync(Guid? categoryId)
    {
        _selectedCategoryId = categoryId is null || categoryId == Guid.Empty ? null : categoryId;
        MarcarCategoriaSelecionada();
        SuggestionIndex = -1;
        await ReloadProductsNowAsync();
    }

    private void MarcarCategoriaSelecionada()
    {
        var selecionada = _selectedCategoryId ?? Guid.Empty;
        foreach (var category in Categories)
            category.IsSelected = category.Id == selecionada;
    }

    /// <summary>
    /// Lista os produtos vendáveis da categoria e do texto de busca atuais.
    /// </summary>
    private async Task LoadProductsAsync()
    {
        var categoria = _exibirCategorias ? _selectedCategoryId : null;
        var items = await _catalogService.GetSellableProductsAsync(categoria, SearchText);
        var selected = _suggestionIndex;
        Products.Clear();
        foreach (var item in items)
            Products.Add(item);

        if (selected >= Products.Count)
            SuggestionIndex = -1;
        else if (_suggestionIndex != selected)
            SuggestionIndex = selected;

        SyncSuggestionsOpen();
        if (_suggestionsOpen && _suggestionIndex >= 0)
            RaisePropertyChanged(nameof(SuggestionIndex));
    }

    /// <summary>
    /// Recarrega a grade e cancela uma busca ainda aguardando o intervalo da digitação.
    /// </summary>
    private Task ReloadProductsNowAsync()
    {
        _productSearchGeneration++;
        return LoadProductsAsync();
    }

    /// <summary>
    /// Espera um instante para não consultar o catálogo a cada tecla.
    /// </summary>
    private void ScheduleProductSearch()
    {
        var generation = ++_productSearchGeneration;
        _ = DelayedProductSearchAsync(generation);
    }

    private async Task DelayedProductSearchAsync(int generation)
    {
        await Task.Delay(200);
        if (generation != _productSearchGeneration)
            return;

        await LoadProductsAsync();
    }

    /// <summary>
    /// Abre a lista a partir do terceiro caractere, salvo quando o operador a dispensou.
    /// </summary>
    private void SyncSuggestionsOpen()
    {
        var open = !_suggestionsDismissed && SearchText.Trim().Length >= 3;
        if (_suggestionsOpen != open)
        {
            _suggestionsOpen = open;
            RaisePropertyChanged(nameof(SuggestionsOpen));
        }

        if (!open)
            SuggestionIndex = -1;

        RaisePropertyChanged(nameof(SuggestionsListVisibility));
        RaisePropertyChanged(nameof(SuggestionsEmptyVisibility));
    }

    /// <summary>
    /// Move o destaque. Para baixo entra no primeiro item; para cima, no primeiro, tira o destaque.
    /// </summary>
    private void MoveSuggestion(object? parameter)
    {
        var step = SuggestionStep(parameter);
        if (step == 0 || SearchText.Trim().Length < 3 || Products.Count == 0)
            return;

        _suggestionsDismissed = false;
        if (!_suggestionsOpen)
        {
            _suggestionsOpen = true;
            RaisePropertyChanged(nameof(SuggestionsOpen));
            RaisePropertyChanged(nameof(SuggestionsListVisibility));
            RaisePropertyChanged(nameof(SuggestionsEmptyVisibility));
        }

        var down = step > 0;
        if (!down && _suggestionIndex < 0)
            return;

        if (_suggestionIndex < 0)
        {
            SuggestionIndex = 0;
            return;
        }

        var next = _suggestionIndex + (down ? 1 : -1);
        if (next < 0)
            SuggestionIndex = -1;
        else if (next >= Products.Count)
            SuggestionIndex = Products.Count - 1;
        else
            SuggestionIndex = next;
    }

    private static int SuggestionStep(object? parameter)
    {
        return parameter switch
        {
            int value => value,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

    /// <summary>
    /// Fecha a lista e tira o destaque. O texto e o filtro da grade permanecem.
    /// </summary>
    private void DismissSuggestions()
    {
        if (!_suggestionsOpen)
            return;

        _suggestionsDismissed = true;
        SyncSuggestionsOpen();
    }

    /// <summary>
    /// Lança o produto da lista e limpa a busca para a próxima leitura.
    /// </summary>
    private async Task AcceptSuggestionAsync(ProductDto? product)
    {
        if (product is null || !IsCashOpen)
            return;

        AddProduct(product);
        await ClearSearchAsync();
    }

    private async Task ClearSearchAsync()
    {
        _suppressSearchSchedule = true;
        try
        {
            SearchText = string.Empty;
        }
        finally
        {
            _suppressSearchSchedule = false;
        }

        await ReloadProductsNowAsync();
    }

    /// <summary>
    /// Soma uma unidade ao item já no carrinho ou inclui uma linha nova. Exige caixa aberto.
    /// </summary>
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

    /// <summary>
    /// Altera a quantidade do item. Zero ou menos remove a linha. Confirma edição em andamento.
    /// </summary>
    private void ChangeQty(CartLineViewModel? item, decimal delta)
    {
        if (item is null)
            return;

        if (item.IsEditingQuantity)
            CommitQuantity(item);

        if (!Cart.Contains(item))
            return;

        var next = item.Quantity + delta;
        if (next <= 0)
            Cart.Remove(item);
        else
            item.Quantity = next;

        RefreshCartTotals();
    }

    /// <summary>
    /// Começa a edição da quantidade e confirma qualquer outra linha que ainda esteja em edição.
    /// </summary>
    private void BeginEditQuantity(CartLineViewModel? item)
    {
        if (item is null || !IsCashOpen)
            return;

        foreach (var other in Cart.Where(line => !ReferenceEquals(line, item) && line.IsEditingQuantity).ToList())
            CommitQuantity(other);

        if (!Cart.Contains(item))
            return;

        item.BeginQuantityEdit();
    }

    /// <summary>
    /// Grava a quantidade digitada. Valor inválido é ignorado; zero ou menos remove o item.
    /// </summary>
    private void CommitQuantity(CartLineViewModel? item)
    {
        if (item is null || !item.IsEditingQuantity)
            return;

        item.IsEditingQuantity = false;

        if (!TryParseMoney(item.QuantityText, out var quantity))
        {
            StatusMessage = "Quantidade inválida.";
            return;
        }

        quantity = decimal.Round(quantity, 3);
        if (quantity <= 0)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                Cart.Remove(item);
                RefreshCartTotals();
                return;
            }

            dispatcher.BeginInvoke(() =>
            {
                Cart.Remove(item);
                RefreshCartTotals();
            });
            return;
        }

        item.Quantity = quantity;
        RefreshCartTotals();
    }

    /// <summary>
    /// Sai da edição e restaura o texto da quantidade original.
    /// </summary>
    private void CancelQuantityEdit(CartLineViewModel? item)
    {
        if (item is null)
            return;

        item.IsEditingQuantity = false;
        item.QuantityText = item.QuantityLabel;
    }

    /// <summary>
    /// Remove o item do carrinho e atualiza os totais.
    /// </summary>
    private void RemoveItem(CartLineViewModel? item)
    {
        if (item is null)
            return;

        Cart.Remove(item);
        RefreshCartTotals();
    }

    /// <summary>
    /// Esvazia o carrinho, o desconto e as parcelas e fecha o painel de pagamento.
    /// </summary>
    private void ClearCart()
    {
        Cart.Clear();
        DiscountAmount = 0;
        SurchargeAmount = 0;
        PaymentLines.Clear();
        RefreshCartTotals();
        ClosePaymentMethodPanel();
        PaymentPanelVisibility = "Collapsed";
    }

    /// <summary>
    /// Descarta as formas lançadas e fecha a efetivação.
    /// </summary>
    private void CancelPaymentPanel()
    {
        PaymentLines.Clear();
        ClosePaymentMethodPanel();
        PaymentPanelVisibility = "Collapsed";
        RaisePaymentFacts();
    }

    /// <summary>
    /// Abre a efetivação com desconto, acréscimo e a lista de formas ainda vazia.
    /// </summary>
    private void StartPayment()
    {
        PaymentLines.Clear();
        DiscountAmount = Math.Min(DiscountAmount, CartTotal);
        CpfConsumidor = string.Empty;
        try
        {
            EmitirCupomFiscal = _nfceService.EmitirPorPadraoAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            EmitirCupomFiscal = false;
        }

        ClosePaymentMethodPanel();
        PaymentPanelVisibility = "Visible";
        RaisePaymentFacts();
    }

    /// <summary>
    /// Abre a escolha da forma de pagamento enquanto ainda houver saldo.
    /// </summary>
    private void OpenPaymentMethodPanel()
    {
        if (!IsDiscountClearedForPayment)
        {
            StatusMessage = "Autorize o desconto antes de lançar o pagamento.";
            DiscountAuthMessage = StatusMessage;
            return;
        }

        if (PaymentRemaining <= 0.009m)
        {
            StatusMessage = "Não há saldo restante. Clique em Gravar para concluir.";
            return;
        }

        _isEnteringPaymentAmount = false;
        PaymentMethodPanelVisibility = "Visible";
    }

    /// <summary>
    /// Preenche o valor da forma escolhida com o saldo restante.
    /// </summary>
    private void SelectPaymentMethod(object? parameter)
    {
        if (parameter is not PaymentMethodButtonViewModel method)
            return;

        SelectedPaymentMethod = method;
        PaymentEntryAmount = PaymentRemaining;
        PaymentReceivedAmount = PaymentRemaining;
        PaymentEntryMessage = string.Empty;
        _isEnteringPaymentAmount = true;
        RaisePropertyChanged(nameof(PaymentMethodListVisibility));
        RaisePropertyChanged(nameof(PaymentAmountEntryVisibility));
    }

    /// <summary>
    /// Confirma o valor da forma e volta para a lista da efetivação.
    /// </summary>
    private void AcceptPaymentAmount()
    {
        if (!IsDiscountClearedForPayment)
        {
            StatusMessage = "Autorize o desconto antes de lançar o pagamento.";
            DiscountAuthMessage = StatusMessage;
            return;
        }

        var amount = decimal.Round(PaymentEntryAmount, 2);
        if (amount <= 0)
        {
            PaymentEntryMessage = "Informe um valor maior que zero.";
            return;
        }

        if (amount - PaymentRemaining > 0.009m)
        {
            PaymentEntryMessage = "O valor não pode ser maior que o saldo restante.";
            return;
        }

        if (SelectedPaymentMethod is not { } method)
            return;

        decimal? received = null;
        if (method.CalculaTroco)
        {
            received = decimal.Round(PaymentReceivedAmount, 2);
            if (received < amount)
            {
                PaymentEntryMessage = "O valor recebido não pode ser menor que o valor pago.";
                return;
            }
        }

        PaymentLines.Add(new PaymentLineViewModel
        {
            FormaPagamentoId = method.Id,
            MethodLabel = method.Descricao,
            CalculaTroco = method.CalculaTroco,
            Amount = amount,
            AmountReceived = received
        });

        ClosePaymentMethodPanel();
        StatusMessage = PaymentRemaining <= 0.009m
            ? "Valor distribuído. Clique em Gravar para concluir."
            : $"Restam {PaymentRemaining:C} para distribuir.";
    }

    /// <summary>
    /// Fecha a escolha da forma sem lançar valor.
    /// </summary>
    private void ClosePaymentMethodPanel()
    {
        _isEnteringPaymentAmount = false;
        PaymentEntryMessage = string.Empty;
        PaymentMethodPanelVisibility = "Collapsed";
    }

    /// <summary>
    /// Volta da digitação do valor para a lista de formas disponíveis.
    /// </summary>
    private void BackToPaymentMethods()
    {
        _isEnteringPaymentAmount = false;
        RaisePropertyChanged(nameof(PaymentMethodListVisibility));
        RaisePropertyChanged(nameof(PaymentAmountEntryVisibility));
    }

    /// <summary>
    /// Remove uma forma lançada e devolve o valor ao saldo restante.
    /// </summary>
    private void RemovePaymentLine(PaymentLineViewModel? line)
    {
        if (line is null)
            return;

        PaymentLines.Remove(line);
        StatusMessage = PaymentRemaining <= 0.009m
            ? "Valor distribuído. Clique em Gravar para concluir."
            : $"Restam {PaymentRemaining:C} para distribuir.";
    }

    /// <summary>
    /// Converte a parcela da tela para o rascunho aceito pelo serviço de venda.
    /// </summary>
    private static PaymentDraftDto ToDraft(PaymentLineViewModel line)
        => new(line.FormaPagamentoId, line.MethodLabel, line.CalculaTroco, line.Amount, line.AmountReceived);

    /// <summary>
    /// Valida o desconto ao sair do campo, se usuário e PIN já estiverem preenchidos.
    /// </summary>
    public void TryAuthorizeDiscountFromFocus()
    {
        if (string.IsNullOrWhiteSpace(DiscountAuthorizerUsername) || string.IsNullOrWhiteSpace(DiscountAuthorizerPin))
            return;

        AuthorizeDiscount();
    }

    /// <summary>
    /// Confere usuário e PIN de gerente ou administrador antes de aceitar o desconto.
    /// </summary>
    private void AuthorizeDiscount()
    {
        if (DiscountAmount <= 0 || _saleService.CurrentUserCanApplyDiscount)
            return;

        if (string.IsNullOrWhiteSpace(DiscountAuthorizerUsername) || string.IsNullOrWhiteSpace(DiscountAuthorizerPin))
        {
            DiscountAuthorized = false;
            DiscountAuthMessage = "Informe o usuário e o PIN do gerente ou administrador.";
            StatusMessage = DiscountAuthMessage;
            return;
        }

        try
        {
            _saleService.ValidateDiscountAuthorization(DiscountAuthorizerUsername, DiscountAuthorizerPin);
            DiscountAuthorized = true;
            DiscountAuthMessage = $"Desconto autorizado por {DiscountAuthorizerUsername.Trim()}.";
            StatusMessage = DiscountAuthMessage;
        }
        catch (DomainException ex)
        {
            DiscountAuthorized = false;
            DiscountAuthMessage = ex.Message;
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// Invalida a autorização quando o valor, o usuário ou o PIN mudam.
    /// </summary>
    private void InvalidateDiscountAuthorization()
    {
        DiscountAuthorized = false;
        if (DiscountAmount > 0 && !_saleService.CurrentUserCanApplyDiscount)
            DiscountAuthMessage = "Informe usuário e PIN. A validação ocorre ao sair do campo ou em Autorizar.";
    }

    /// <summary>
    /// Indica se o pagamento pode seguir: sem desconto, operador autorizado ou desconto já validado.
    /// </summary>
    private bool IsDiscountClearedForPayment =>
        DiscountAmount <= 0 || _saleService.CurrentUserCanApplyDiscount || DiscountAuthorized;

    /// <summary>
    /// Indica se ainda há saldo e o desconto já está liberado para escolher uma forma.
    /// </summary>
    private bool CanOpenPaymentMethod() => PaymentRemaining > 0.009m && IsDiscountClearedForPayment;

    /// <summary>
    /// Indica se as formas lançadas fecham o total e o desconto já está liberado.
    /// </summary>
    private bool CanSavePayment() =>
        !_nfceEnvioEmAndamento
        && PaymentLines.Count > 0
        && Math.Abs(PayableTotal - PaymentPaid) <= 0.009m
        && IsDiscountClearedForPayment;

    /// <summary>
    /// Avisa quando as formas já lançadas passam do total depois de um ajuste.
    /// </summary>
    private void NotifyAllocationDrift()
    {
        if (PaymentLines.Count > 0 && PaymentPaid - PayableTotal > 0.009m)
            StatusMessage = "As formas lançadas passam do total. Remova alguma para continuar.";
    }

    /// <summary>
    /// Reavalia os comandos de pagamento depois de mudança de desconto, acréscimo ou autorização.
    /// </summary>
    private void RaisePaymentCommandStates()
    {
        OpenPaymentMethodCommand.RaiseCanExecuteChanged();
        SavePaymentCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Conclui a venda, limpa o carrinho, atualiza o estoque visível e abre o ticket.
    /// </summary>
    private async Task CompleteSaleAsync(IReadOnlyList<PaymentDraftDto> payments)
    {
        if (!IsDiscountClearedForPayment)
        {
            StatusMessage = "Autorize o desconto antes de concluir a venda.";
            DiscountAuthMessage = StatusMessage;
            return;
        }

        var emitirCupom = EmitirCupomFiscal;
        if (emitirCupom)
            DefinirEnvioNfce(true, "Preparando a NFC-e");

        try
        {
            var cart = Cart.Select(c => new CartItemDto(
                c.ProductId,
                c.ProductName,
                c.ProductSku,
                c.Quantity,
                c.UnitPrice,
                c.Subtotal)).ToList();

            var descontoUsuario = DiscountAmount > 0 && !_saleService.CurrentUserCanApplyDiscount ? DiscountAuthorizerUsername : null;
            var descontoPin = DiscountAmount > 0 && !_saleService.CurrentUserCanApplyDiscount ? DiscountAuthorizerPin : null;
            var progresso = new Progress<string>(mensagem => NfceEnvioMensagem = mensagem);
            var sale = emitirCupom
                ? await _nfceService.ConcluirVendaAsync(
                    cart,
                    payments,
                    DiscountAmount,
                    descontoUsuario,
                    descontoPin,
                    SurchargeAmount,
                    CpfConsumidor,
                    progresso)
                : await _saleService.CompleteSaleAsync(
                    cart,
                    payments,
                    DiscountAmount,
                    descontoUsuario,
                    descontoPin,
                    SurchargeAmount);

            LastTicketInfo =
                $"Ticket {sale.TicketNumber} · {sale.Total:C}" +
                (sale.ChangeAmount > 0 ? $" · Troco {sale.ChangeAmount:C}" : string.Empty);

            Cart.Clear();
            PaymentLines.Clear();
            DiscountAmount = 0;
            SurchargeAmount = 0;
            DiscountAuthorizerPin = string.Empty;
            CpfConsumidor = string.Empty;
            RefreshCartTotals();
            ClosePaymentMethodPanel();
            PaymentPanelVisibility = "Collapsed";
            StatusMessage = $"Venda concluída: {LastTicketInfo}";
            await ReloadProductsNowAsync();
            await ShowTicketAsync(sale.Id, isReprint: false);
            if (emitirCupom)
            {
                NfceEnvioMensagem = "Imprimindo o cupom";
                await PrintCurrentTicketAsync();
            }
        }
        catch (DomainException ex)
        {
            DefinirEnvioNfce(false);
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "AcaiPos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            DefinirEnvioNfce(false);
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "AcaiPos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DefinirEnvioNfce(false);
        }
    }

    private void DefinirEnvioNfce(bool emAndamento, string? mensagem = null)
    {
        _nfceEnvioEmAndamento = emAndamento;
        if (mensagem is not null)
            NfceEnvioMensagem = mensagem;
        RaisePropertyChanged(nameof(NfceEnvioVisivel));
        SavePaymentCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Lança o item destacado. Sem destaque, um código de barras exato entra no carrinho; o resto só filtra a grade.
    /// </summary>
    private async Task SearchOrBarcodeAsync()
    {
        if (_suggestionsOpen && _suggestionIndex >= 0 && _suggestionIndex < Products.Count)
        {
            await AcceptSuggestionAsync(Products[_suggestionIndex]);
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await ReloadProductsNowAsync();
            return;
        }

        var byBarcode = await _catalogService.FindByBarcodeAsync(SearchText.Trim());
        if (byBarcode is not null)
        {
            AddProduct(byBarcode);
            await ClearSearchAsync();
            return;
        }

        await ReloadProductsNowAsync();
    }

    /// <summary>
    /// Acrescenta um dígito ao PIN, até o limite de oito caracteres.
    /// </summary>
    private void AppendPin(string? digit)
    {
        if (string.IsNullOrWhiteSpace(digit) || Pin.Length >= 8)
            return;

        Pin += digit;
    }

    /// <summary>
    /// Notifica totais, restante, troco e o estado dos comandos de pagamento.
    /// </summary>
    private void RaisePaymentFacts()
    {
        RaisePropertyChanged(nameof(PayableTotal));
        RaisePropertyChanged(nameof(PayableTotalLabel));
        RaisePropertyChanged(nameof(PaymentPaid));
        RaisePropertyChanged(nameof(PaymentPaidLabel));
        RaisePropertyChanged(nameof(PaymentRemaining));
        RaisePropertyChanged(nameof(PaymentRemainingLabel));
        RaisePropertyChanged(nameof(PaymentBalanceLabel));
        RaisePropertyChanged(nameof(DiscountAuthVisibility));
        RaisePaymentCommandStates();
    }

    /// <summary>
    /// Recalcula o total do carrinho e os valores derivados do pagamento.
    /// </summary>
    private void RefreshCartTotals()
    {
        RaisePropertyChanged(nameof(CartTotal));
        RaisePropertyChanged(nameof(CartTotalLabel));
        RaisePropertyChanged(nameof(CartCountLabel));
        RaisePaymentFacts();
        StartPaymentCommand.RaiseCanExecuteChanged();
        ClearCartCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Carrega o texto do ticket e abre o painel de visualização.
    /// </summary>
    private async Task ShowTicketAsync(Guid saleId, bool isReprint)
    {
        var ticket = await _ticketService.GetTicketAsync(saleId, isReprint);
        _currentTicketSaleId = ticket.SaleId;
        var titulo = ticket.TemCupomFiscal ? "NFC-e" : "Ticket";
        TicketPanelTitle = isReprint ? $"Reimpressão · {titulo} {ticket.TicketNumber}" : $"{titulo} · {ticket.TicketNumber}";
        TicketPreviewText = _ticketService.Format(ticket);
        TicketQrImage = string.IsNullOrWhiteSpace(ticket.QrCode) ? null : QrCodeImagem.ParaTela(ticket.QrCode);
        ShowReprintPanel = false;
        ShowTicketPanel = true;
        PrintTicketCommand.RaiseCanExecuteChanged();
        LastTicketInfo = $"Ticket {ticket.TicketNumber} · {ticket.Total:C}";
    }

    /// <summary>
    /// Fecha o painel do ticket e libera a impressão.
    /// </summary>
    private void CloseTicketPanel()
    {
        ShowTicketPanel = false;
        _currentTicketSaleId = null;
        TicketPreviewText = string.Empty;
        TicketQrImage = null;
        PrintTicketCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Envia o ticket aberto para a impressora, como original ou reimpressão.
    /// </summary>
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

    /// <summary>
    /// Lista os tickets de hoje e abre o painel de reimpressão.
    /// </summary>
    private async Task OpenReprintPanelAsync()
    {
        ShowCashMenu = false;
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

    /// <summary>
    /// Abre o ticket selecionado no modo de reimpressão.
    /// </summary>
    private async Task ViewTicketAsync(TicketDto? ticket)
    {
        if (ticket is null)
            return;

        await ShowTicketAsync(ticket.SaleId, isReprint: true);
    }

    /// <summary>
    /// Abre o ticket selecionado e envia a reimpressão.
    /// </summary>
    private async Task ReprintTicketAsync(TicketDto? ticket)
    {
        if (ticket is null)
            return;

        await ShowTicketAsync(ticket.SaleId, isReprint: true);
        await PrintCurrentTicketAsync();
    }
}
