using System.Collections.ObjectModel;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Domain.Common;

namespace AcaiPos.Desktop.ViewModels;

/// <summary>
/// Cadastro de produtos e categorias: abas, filtros, formulários e desativação.
/// </summary>
public sealed class CatalogManagementViewModel : ObservableObject
{
    private readonly CatalogService _catalogService;
    private readonly InscricaoTerminalService _inscricao;
    private readonly Action<string>? _notify;
    private readonly Func<Task>? _onCatalogChanged;

    private string _activeTab = "Products";
    private string _productSearch = string.Empty;
    private Guid? _filterCategoryId;
    private bool _includeInactive = true;
    private string _editorMessage = string.Empty;

    private Guid? _editingCategoryId;
    private string _categoryName = string.Empty;
    private string _categoryDescription = string.Empty;
    private int _categorySortOrder;
    private bool _categoryIsActive = true;
    private bool _categoryIsDefault;

    private Guid? _editingProductId;
    private string _productName = string.Empty;
    private string _productSku = string.Empty;
    private string _productBarcode = string.Empty;
    private decimal _productSalePrice;
    private decimal? _productCostPrice;
    private decimal _productStock;
    private string _productUnit = "UN";
    private bool _productIsActive = true;
    private string _productNcm = string.Empty;
    private string _productCfop = "5102";
    private int _productOrigem;
    private string _productCsosn = "102";
    private string _productCest = string.Empty;
    private string _productPisCst = "49";
    private string _productCofinsCst = "49";
    private string _productIbsCbsCst = "000";
    private string _productCClassTrib = "000001";

    /// <summary>
    /// Liga os comandos de abas, busca e edição de categorias e produtos.
    /// </summary>
    public CatalogManagementViewModel(
        CatalogService catalogService,
        InscricaoTerminalService inscricao,
        Action<string>? notify = null,
        Func<Task>? onCatalogChanged = null)
    {
        _catalogService = catalogService;
        _inscricao = inscricao;
        _notify = notify;
        _onCatalogChanged = onCatalogChanged;

        ShowProductsTabCommand = new RelayCommand(_ => ActiveTab = "Products");
        ShowCategoriesTabCommand = new RelayCommand(_ => ActiveTab = "Categories");
        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        SearchProductsCommand = new RelayCommand(async _ => await LoadProductsAsync());

        NewCategoryCommand = new RelayCommand(_ => ResetCategoryForm());
        EditCategoryCommand = new RelayCommand(p => BeginEditCategory(p as CategoryManageDto));
        SaveCategoryCommand = new RelayCommand(async _ => await SaveCategoryAsync());
        DeactivateCategoryCommand = new RelayCommand(async p => await DeactivateCategoryAsync(p as CategoryManageDto));

        NewProductCommand = new RelayCommand(_ => ResetProductForm());
        EditProductCommand = new RelayCommand(p => BeginEditProduct(p as ProductManageDto));
        SaveProductCommand = new RelayCommand(async _ => await SaveProductAsync());
        DeactivateProductCommand = new RelayCommand(async p => await DeactivateProductAsync(p as ProductManageDto));
    }

    public bool CadastroMestreTravado { get; private set; }
    public bool CadastroMestreLivre => !CadastroMestreTravado;
    public string AvisoCadastro => CadastroMestreTravado
        ? "Cadastro mestre na central. Neste terminal só o preço de venda e o estoque podem mudar."
        : string.Empty;

    public ObservableCollection<CategoryManageDto> ManagedCategories { get; } = [];
    public ObservableCollection<CategoryManageDto> FilterCategoryOptions { get; } = [];
    public ObservableCollection<ProductManageDto> ManagedProducts { get; } = [];
    public ObservableCollection<ProductCategoryOption> ProductCategoryOptions { get; } = [];

    public RelayCommand ShowProductsTabCommand { get; }
    public RelayCommand ShowCategoriesTabCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchProductsCommand { get; }
    public RelayCommand NewCategoryCommand { get; }
    public RelayCommand EditCategoryCommand { get; }
    public RelayCommand SaveCategoryCommand { get; }
    public RelayCommand DeactivateCategoryCommand { get; }
    public RelayCommand NewProductCommand { get; }
    public RelayCommand EditProductCommand { get; }
    public RelayCommand SaveProductCommand { get; }
    public RelayCommand DeactivateProductCommand { get; }

    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            SetProperty(ref _activeTab, value);
            RaisePropertyChanged(nameof(IsProductsTab));
            RaisePropertyChanged(nameof(IsCategoriesTab));
            RaisePropertyChanged(nameof(ProductsTabVisibility));
            RaisePropertyChanged(nameof(CategoriesTabVisibility));
        }
    }

    public bool IsProductsTab => ActiveTab == "Products";
    public bool IsCategoriesTab => ActiveTab == "Categories";
    public string ProductsTabVisibility => IsProductsTab ? "Visible" : "Collapsed";
    public string CategoriesTabVisibility => IsCategoriesTab ? "Visible" : "Collapsed";

    public string ProductSearch
    {
        get => _productSearch;
        set => SetProperty(ref _productSearch, value);
    }

    public Guid? FilterCategoryId
    {
        get => _filterCategoryId;
        set => SetProperty(ref _filterCategoryId, value);
    }

    public bool IncludeInactive
    {
        get => _includeInactive;
        set => SetProperty(ref _includeInactive, value);
    }

    public string EditorMessage
    {
        get => _editorMessage;
        set => SetProperty(ref _editorMessage, value);
    }

    public string CategoryEditorTitle => _editingCategoryId is null ? "Nova categoria" : "Editar categoria";
    public string ProductEditorTitle => _editingProductId is null ? "Novo produto" : "Editar produto";

    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public string CategoryDescription
    {
        get => _categoryDescription;
        set => SetProperty(ref _categoryDescription, value);
    }

    public int CategorySortOrder
    {
        get => _categorySortOrder;
        set => SetProperty(ref _categorySortOrder, value);
    }

    public bool CategoryIsActive
    {
        get => _categoryIsActive;
        set => SetProperty(ref _categoryIsActive, value);
    }

    public bool CategoryIsDefault
    {
        get => _categoryIsDefault;
        set => SetProperty(ref _categoryIsDefault, value);
    }

    public string ProductName
    {
        get => _productName;
        set => SetProperty(ref _productName, value);
    }

    public string ProductSku
    {
        get => _productSku;
        set => SetProperty(ref _productSku, value);
    }

    public string ProductBarcode
    {
        get => _productBarcode;
        set => SetProperty(ref _productBarcode, value);
    }

    public decimal ProductSalePrice
    {
        get => _productSalePrice;
        set => SetProperty(ref _productSalePrice, value);
    }

    public decimal? ProductCostPrice
    {
        get => _productCostPrice;
        set => SetProperty(ref _productCostPrice, value);
    }

    public decimal ProductStock
    {
        get => _productStock;
        set => SetProperty(ref _productStock, value);
    }

    public string ProductUnit
    {
        get => _productUnit;
        set => SetProperty(ref _productUnit, value);
    }

    public string ProductNcm
    {
        get => _productNcm;
        set => SetProperty(ref _productNcm, value);
    }

    public string ProductCfop
    {
        get => _productCfop;
        set => SetProperty(ref _productCfop, value);
    }

    public int ProductOrigem
    {
        get => _productOrigem;
        set => SetProperty(ref _productOrigem, value);
    }

    public string ProductCsosn
    {
        get => _productCsosn;
        set => SetProperty(ref _productCsosn, value);
    }

    public string ProductCest
    {
        get => _productCest;
        set => SetProperty(ref _productCest, value);
    }

    public string ProductPisCst
    {
        get => _productPisCst;
        set => SetProperty(ref _productPisCst, value);
    }

    public string ProductCofinsCst
    {
        get => _productCofinsCst;
        set => SetProperty(ref _productCofinsCst, value);
    }

    public string ProductIbsCbsCst
    {
        get => _productIbsCbsCst;
        set => SetProperty(ref _productIbsCbsCst, value);
    }

    public string ProductCClassTrib
    {
        get => _productCClassTrib;
        set => SetProperty(ref _productCClassTrib, value);
    }

    public bool ProductIsActive
    {
        get => _productIsActive;
        set => SetProperty(ref _productIsActive, value);
    }

    /// <summary>
    /// Carrega categorias e produtos e limpa os dois formulários.
    /// </summary>
    public async Task LoadAsync()
    {
        var atual = await _inscricao.ObterAsync();
        CadastroMestreTravado = atual?.TemSegredo == true
            && !string.IsNullOrWhiteSpace(atual.EnderecoApi)
            && atual.LojaId != Guid.Empty
            && atual.TerminalId != Guid.Empty;
        RaisePropertyChanged(nameof(CadastroMestreTravado));
        RaisePropertyChanged(nameof(CadastroMestreLivre));
        RaisePropertyChanged(nameof(AvisoCadastro));
        await LoadCategoriesAsync();
        await LoadProductsAsync();
        ResetCategoryForm();
        ResetProductForm();
        EditorMessage = "Catálogo carregado.";
    }

    public async Task AtualizarListasAsync()
    {
        var editando = _editingProductId;
        await LoadCategoriesAsync();
        await LoadProductsAsync();
        if (editando is not Guid id)
            return;

        var produto = ManagedProducts.FirstOrDefault(item => item.Id == id);
        if (produto is null)
            return;

        ProductSalePrice = produto.SalePrice;
        ProductStock = produto.StockQuantity;
    }

    /// <summary>
    /// Recarrega a lista de categorias e as opções do filtro, incluindo "Todas".
    /// </summary>
    private async Task LoadCategoriesAsync()
    {
        ManagedCategories.Clear();
        FilterCategoryOptions.Clear();
        FilterCategoryOptions.Add(new CategoryManageDto(Guid.Empty, "Todas", null, 0, true));

        foreach (var category in await _catalogService.GetManagedCategoriesAsync())
        {
            ManagedCategories.Add(category);
            FilterCategoryOptions.Add(category);
        }

        FilterCategoryId ??= Guid.Empty;
    }

    /// <summary>
    /// Recarrega os produtos conforme categoria, texto e inclusão de inativos.
    /// </summary>
    private async Task LoadProductsAsync()
    {
        ManagedProducts.Clear();
        var items = await _catalogService.GetManagedProductsAsync(
            FilterCategoryId,
            ProductSearch,
            IncludeInactive);

        foreach (var item in items)
            ManagedProducts.Add(item);
    }

    /// <summary>
    /// Limpa o formulário para uma categoria nova, sugerindo a próxima ordem.
    /// </summary>
    private void ResetCategoryForm()
    {
        _editingCategoryId = null;
        CategoryName = string.Empty;
        CategoryDescription = string.Empty;
        CategorySortOrder = (ManagedCategories.Count == 0 ? 0 : ManagedCategories.Max(c => c.SortOrder)) + 1;
        CategoryIsActive = true;
        CategoryIsDefault = false;
        RaisePropertyChanged(nameof(CategoryEditorTitle));
        EditorMessage = "Preencha os dados da categoria.";
    }

    /// <summary>
    /// Preenche o formulário com a categoria selecionada.
    /// </summary>
    private void BeginEditCategory(CategoryManageDto? category)
    {
        if (category is null)
            return;

        _editingCategoryId = category.Id;
        CategoryName = category.Name;
        CategoryDescription = category.Description ?? string.Empty;
        CategorySortOrder = category.SortOrder;
        CategoryIsActive = category.IsActive;
        CategoryIsDefault = category.IsDefault;
        RaisePropertyChanged(nameof(CategoryEditorTitle));
        EditorMessage = $"Editando categoria {category.Name}.";
    }

    /// <summary>
    /// Cria ou atualiza a categoria e avisa a tela de venda para recarregar o catálogo.
    /// </summary>
    private async Task SaveCategoryAsync()
    {
        try
        {
            var request = new SaveCategoryRequest(
                CategoryName,
                CategoryDescription,
                CategorySortOrder,
                CategoryIsActive,
                CategoryIsDefault);

            if (_editingCategoryId is null)
                await _catalogService.CreateCategoryAsync(request);
            else
                await _catalogService.UpdateCategoryAsync(_editingCategoryId.Value, request);

            await LoadCategoriesAsync();
            await LoadProductsAsync();
            ResetCategoryForm();
            EditorMessage = "Categoria salva.";
            _notify?.Invoke("Categoria salva.");
            if (_onCatalogChanged is not null)
                await _onCatalogChanged();
        }
        catch (DomainException ex)
        {
            EditorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            EditorMessage = $"Erro: {ex.Message}";
        }
    }

    /// <summary>
    /// Desativa a categoria e recarrega listas e tela de venda.
    /// </summary>
    private async Task DeactivateCategoryAsync(CategoryManageDto? category)
    {
        if (category is null)
            return;

        try
        {
            await _catalogService.DeactivateCategoryAsync(category.Id);
            await LoadCategoriesAsync();
            await LoadProductsAsync();
            if (_editingCategoryId == category.Id)
                ResetCategoryForm();
            EditorMessage = $"Categoria {category.Name} desativada.";
            _notify?.Invoke(EditorMessage);
            if (_onCatalogChanged is not null)
                await _onCatalogChanged();
        }
        catch (DomainException ex)
        {
            EditorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Limpa o formulário para um produto novo e reconstrói as categorias marcáveis.
    /// </summary>
    private void ResetProductForm()
    {
        _editingProductId = null;
        ProductName = string.Empty;
        ProductSku = string.Empty;
        ProductBarcode = string.Empty;
        RebuildProductCategoryOptions();
        ProductSalePrice = 0;
        ProductCostPrice = null;
        ProductStock = 0;
        ProductUnit = "UN";
        ProductIsActive = true;
        ProductNcm = string.Empty;
        ProductCfop = "5102";
        ProductOrigem = 0;
        ProductCsosn = "102";
        ProductCest = string.Empty;
        ProductPisCst = "49";
        ProductCofinsCst = "49";
        ProductIbsCbsCst = "000";
        ProductCClassTrib = "000001";
        RaisePropertyChanged(nameof(ProductEditorTitle));
        EditorMessage = "Preencha os dados do produto.";
    }

    /// <summary>
    /// Preenche o formulário com o produto selecionado e marca as categorias dele.
    /// </summary>
    private void BeginEditProduct(ProductManageDto? product)
    {
        if (product is null)
            return;

        _editingProductId = product.Id;
        ProductName = product.Name;
        ProductSku = product.Sku;
        ProductBarcode = product.Barcode ?? string.Empty;
        RebuildProductCategoryOptions(product.CategoryIds);
        ProductSalePrice = product.SalePrice;
        ProductCostPrice = product.CostPrice;
        ProductStock = product.StockQuantity;
        ProductUnit = product.UnitOfMeasure;
        ProductIsActive = product.IsActive;
        ProductNcm = product.Ncm;
        ProductCfop = product.Cfop;
        ProductOrigem = product.Origem;
        ProductCsosn = product.Csosn;
        ProductCest = product.Cest ?? string.Empty;
        ProductPisCst = product.PisCst;
        ProductCofinsCst = product.CofinsCst;
        ProductIbsCbsCst = product.IbsCbsCst;
        ProductCClassTrib = product.CClassTrib;

        RaisePropertyChanged(nameof(ProductEditorTitle));
        EditorMessage = $"Editando produto {product.Name}.";
    }

    /// <summary>
    /// Cria ou atualiza o produto com as categorias marcadas e avisa a tela de venda.
    /// </summary>
    private async Task SaveProductAsync()
    {
        try
        {
            var request = new SaveProductRequest(
                ProductName,
                ProductSku,
                string.IsNullOrWhiteSpace(ProductBarcode) ? null : ProductBarcode,
                ProductCategoryOptions.Where(option => option.IsSelected).Select(option => option.Id).ToList(),
                ProductSalePrice,
                ProductCostPrice,
                ProductStock,
                ProductUnit,
                ProductIsActive,
                ProductNcm,
                ProductCfop,
                ProductOrigem,
                ProductCsosn,
                string.IsNullOrWhiteSpace(ProductCest) ? null : ProductCest,
                ProductPisCst,
                ProductCofinsCst,
                ProductIbsCbsCst,
                ProductCClassTrib);

            if (_editingProductId is null)
                await _catalogService.CreateProductAsync(request);
            else
                await _catalogService.UpdateProductAsync(_editingProductId.Value, request);

            await LoadProductsAsync();
            ResetProductForm();
            EditorMessage = "Produto salvo.";
            _notify?.Invoke("Produto salvo.");
            if (_onCatalogChanged is not null)
                await _onCatalogChanged();
        }
        catch (DomainException ex)
        {
            EditorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            EditorMessage = $"Erro: {ex.Message}";
        }
    }

    /// <summary>
    /// Desativa o produto e recarrega a lista e a tela de venda.
    /// </summary>
    private async Task DeactivateProductAsync(ProductManageDto? product)
    {
        if (product is null)
            return;

        try
        {
            await _catalogService.DeactivateProductAsync(product.Id);
            await LoadProductsAsync();
            if (_editingProductId == product.Id)
                ResetProductForm();
            EditorMessage = $"Produto {product.Name} desativado.";
            _notify?.Invoke(EditorMessage);
            if (_onCatalogChanged is not null)
                await _onCatalogChanged();
        }
        catch (DomainException ex)
        {
            EditorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Remonta as categorias do produto. Inativas só aparecem se já estiverem ligadas a ele.
    /// </summary>
    private void RebuildProductCategoryOptions(IReadOnlyCollection<Guid>? selectedIds = null)
    {
        var selected = selectedIds?.ToHashSet() ?? [];
        ProductCategoryOptions.Clear();

        foreach (var category in ManagedCategories
            .Where(category => category.IsActive || selected.Contains(category.Id))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name))
        {
            ProductCategoryOptions.Add(new ProductCategoryOption(
                category.Id,
                category.Name,
                selected.Contains(category.Id)));
        }
    }
}

/// <summary>
/// Categoria marcável no formulário de produto.
/// </summary>
public sealed class ProductCategoryOption : ObservableObject
{
    private bool _isSelected;

    /// <summary>
    /// Cria a opção com o estado inicial de seleção.
    /// </summary>
    public ProductCategoryOption(Guid id, string name, bool isSelected)
    {
        Id = id;
        Name = name;
        _isSelected = isSelected;
    }

    public Guid Id { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
