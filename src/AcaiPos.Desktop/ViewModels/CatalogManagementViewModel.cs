using System.Collections.ObjectModel;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Domain.Common;

namespace AcaiPos.Desktop.ViewModels;

public sealed class CatalogManagementViewModel : ObservableObject
{
    private readonly CatalogService _catalogService;
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

    private Guid? _editingProductId;
    private string _productName = string.Empty;
    private string _productSku = string.Empty;
    private string _productBarcode = string.Empty;
    private Guid _productCategoryId;
    private decimal _productSalePrice;
    private decimal? _productCostPrice;
    private decimal _productStock;
    private string _productUnit = "UN";
    private bool _productIsActive = true;

    public CatalogManagementViewModel(
        CatalogService catalogService,
        Action<string>? notify = null,
        Func<Task>? onCatalogChanged = null)
    {
        _catalogService = catalogService;
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

    public ObservableCollection<CategoryManageDto> ManagedCategories { get; } = [];
    public ObservableCollection<CategoryManageDto> ActiveCategoryOptions { get; } = [];
    public ObservableCollection<CategoryManageDto> FilterCategoryOptions { get; } = [];
    public ObservableCollection<ProductManageDto> ManagedProducts { get; } = [];

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

    public Guid ProductCategoryId
    {
        get => _productCategoryId;
        set => SetProperty(ref _productCategoryId, value);
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

    public bool ProductIsActive
    {
        get => _productIsActive;
        set => SetProperty(ref _productIsActive, value);
    }

    public async Task LoadAsync()
    {
        await LoadCategoriesAsync();
        await LoadProductsAsync();
        ResetCategoryForm();
        ResetProductForm();
        EditorMessage = "Catálogo carregado.";
    }

    private async Task LoadCategoriesAsync()
    {
        ManagedCategories.Clear();
        ActiveCategoryOptions.Clear();
        FilterCategoryOptions.Clear();
        FilterCategoryOptions.Add(new CategoryManageDto(Guid.Empty, "Todas", null, 0, true));

        foreach (var category in await _catalogService.GetManagedCategoriesAsync())
        {
            ManagedCategories.Add(category);
            FilterCategoryOptions.Add(category);
            if (category.IsActive)
                ActiveCategoryOptions.Add(category);
        }

        FilterCategoryId ??= Guid.Empty;
    }

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

    private void ResetCategoryForm()
    {
        _editingCategoryId = null;
        CategoryName = string.Empty;
        CategoryDescription = string.Empty;
        CategorySortOrder = (ManagedCategories.Count == 0 ? 0 : ManagedCategories.Max(c => c.SortOrder)) + 1;
        CategoryIsActive = true;
        RaisePropertyChanged(nameof(CategoryEditorTitle));
        EditorMessage = "Preencha os dados da categoria.";
    }

    private void BeginEditCategory(CategoryManageDto? category)
    {
        if (category is null)
            return;

        _editingCategoryId = category.Id;
        CategoryName = category.Name;
        CategoryDescription = category.Description ?? string.Empty;
        CategorySortOrder = category.SortOrder;
        CategoryIsActive = category.IsActive;
        RaisePropertyChanged(nameof(CategoryEditorTitle));
        EditorMessage = $"Editando categoria {category.Name}.";
    }

    private async Task SaveCategoryAsync()
    {
        try
        {
            var request = new SaveCategoryRequest(
                CategoryName,
                CategoryDescription,
                CategorySortOrder,
                CategoryIsActive);

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

    private void ResetProductForm()
    {
        _editingProductId = null;
        ProductName = string.Empty;
        ProductSku = string.Empty;
        ProductBarcode = string.Empty;
        ProductCategoryId = ActiveCategoryOptions.FirstOrDefault()?.Id ?? Guid.Empty;
        ProductSalePrice = 0;
        ProductCostPrice = null;
        ProductStock = 0;
        ProductUnit = "UN";
        ProductIsActive = true;
        RaisePropertyChanged(nameof(ProductEditorTitle));
        EditorMessage = "Preencha os dados do produto.";
    }

    private void BeginEditProduct(ProductManageDto? product)
    {
        if (product is null)
            return;

        _editingProductId = product.Id;
        ProductName = product.Name;
        ProductSku = product.Sku;
        ProductBarcode = product.Barcode ?? string.Empty;
        ProductCategoryId = product.CategoryId;
        ProductSalePrice = product.SalePrice;
        ProductCostPrice = product.CostPrice;
        ProductStock = product.StockQuantity;
        ProductUnit = product.UnitOfMeasure;
        ProductIsActive = product.IsActive;

        if (ActiveCategoryOptions.All(c => c.Id != product.CategoryId))
        {
            var category = ManagedCategories.FirstOrDefault(c => c.Id == product.CategoryId);
            if (category is not null)
                ActiveCategoryOptions.Insert(0, category);
        }

        RaisePropertyChanged(nameof(ProductEditorTitle));
        EditorMessage = $"Editando produto {product.Name}.";
    }

    private async Task SaveProductAsync()
    {
        try
        {
            var request = new SaveProductRequest(
                ProductName,
                ProductSku,
                string.IsNullOrWhiteSpace(ProductBarcode) ? null : ProductBarcode,
                ProductCategoryId,
                ProductSalePrice,
                ProductCostPrice,
                ProductStock,
                ProductUnit,
                ProductIsActive);

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
}
