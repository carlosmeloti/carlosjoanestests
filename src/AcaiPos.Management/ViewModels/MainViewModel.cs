using System.Collections.ObjectModel;
using AcaiPos.Management.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcaiPos.Management.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public const int PageSize = 20;

    private int _currentPage = 1;
    private bool _suspendQuery;

    public MainViewModel(string userName)
    {
        UserName = userName;
        UserInitial = string.IsNullOrWhiteSpace(userName)
            ? "?"
            : char.ToUpperInvariant(userName.Trim()[0]).ToString();

        Schemas = SampleCatalog.Schemas;
        _selectedSchema = Schemas[0];
        ApplyHeaders(_selectedSchema);
        Menus = CreateMenus();
        Shortcuts = CreateShortcuts();
        ApplyQuery(resetPage: true);
    }

    public string UserName { get; }

    public string UserInitial { get; }

    public IReadOnlyList<EntitySchema> Schemas { get; }

    public IReadOnlyList<TopMenu> Menus { get; }

    public IReadOnlyList<ShortcutItem> Shortcuts { get; }

    public ObservableCollection<ResultRow> Rows { get; } = [];

    public ObservableCollection<PageNumber> PageNumbers { get; } = [];

    [ObservableProperty]
    private EntitySchema _selectedSchema = null!;

    [ObservableProperty]
    private SearchField _selectedField = SearchField.Id;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _header0 = string.Empty;

    [ObservableProperty]
    private string _header1 = string.Empty;

    [ObservableProperty]
    private string _header2 = string.Empty;

    [ObservableProperty]
    private string _header3 = string.Empty;

    [ObservableProperty]
    private string _header4 = string.Empty;

    [ObservableProperty]
    private int _pageCount = 1;

    [ObservableProperty]
    private bool _canPrevious;

    [ObservableProperty]
    private bool _canNext;

    public int CurrentPage => _currentPage;

    public bool IsCpfEnabled => SelectedSchema.SupportsDocument;

    public string PageSizeLabel => $"{PageSize} por página";

    partial void OnSelectedSchemaChanged(EntitySchema value)
    {
        _suspendQuery = true;
        if (!value.SupportsDocument && SelectedField == SearchField.Cpf)
            SelectedField = SearchField.Id;
        _suspendQuery = false;

        OnPropertyChanged(nameof(IsCpfEnabled));
        ApplyHeaders(value);
        ApplyQuery(resetPage: true);
    }

    partial void OnSelectedFieldChanged(SearchField value)
    {
        if (!_suspendQuery && SelectedSchema is not null)
            ApplyQuery(resetPage: true);
    }

    partial void OnSearchTextChanged(string value)
    {
        if (SelectedSchema is not null)
            ApplyQuery(resetPage: true);
    }

    [RelayCommand]
    private void Previous()
    {
        if (_currentPage <= 1)
            return;

        _currentPage--;
        ApplyQuery(resetPage: false);
    }

    [RelayCommand]
    private void Next()
    {
        if (_currentPage >= PageCount)
            return;

        _currentPage++;
        ApplyQuery(resetPage: false);
    }

    [RelayCommand]
    private void GoToPage(object? parameter)
    {
        if (!int.TryParse(parameter?.ToString(), out var page))
            return;

        if (page < 1 || page > PageCount || page == _currentPage)
            return;

        _currentPage = page;
        ApplyQuery(resetPage: false);
    }

    private void ApplyHeaders(EntitySchema schema)
    {
        Header0 = schema.Headers[0];
        Header1 = schema.Headers[1];
        Header2 = schema.Headers[2];
        Header3 = schema.Headers[3];
        Header4 = schema.Headers[4];
    }

    private void ApplyQuery(bool resetPage)
    {
        if (resetPage)
            _currentPage = 1;

        var filtered = SelectedSchema.Entries.Where(Matches).ToList();
        PageCount = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize));
        if (_currentPage > PageCount)
            _currentPage = PageCount;

        var pageItems = filtered
            .Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        Rows.Clear();
        foreach (var entry in pageItems)
        {
            Rows.Add(new ResultRow
            {
                C0 = entry.Cells[0],
                C1 = entry.Cells[1],
                C2 = entry.Cells[2],
                C3 = entry.Cells[3],
                C4 = entry.Cells[4]
            });
        }

        PageNumbers.Clear();
        foreach (var page in VisiblePages())
        {
            PageNumbers.Add(new PageNumber
            {
                Number = page,
                IsCurrent = page == _currentPage,
                SelectCommand = GoToPageCommand
            });
        }

        CanPrevious = _currentPage > 1;
        CanNext = _currentPage < PageCount;
        OnPropertyChanged(nameof(CurrentPage));
    }

    private bool Matches(CatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var term = SearchText.Trim();
        var value = SelectedField switch
        {
            SearchField.Id => entry.Id,
            SearchField.Name => entry.Name,
            SearchField.Cpf => entry.Document ?? string.Empty,
            _ => string.Empty
        };

        return value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }

    private IEnumerable<int> VisiblePages()
    {
        if (PageCount <= 7)
            return Enumerable.Range(1, PageCount);

        var start = Math.Max(1, _currentPage - 3);
        var end = Math.Min(PageCount, start + 6);
        start = Math.Max(1, end - 6);
        return Enumerable.Range(start, end - start + 1);
    }

    private static IReadOnlyList<TopMenu> CreateMenus() =>
    [
        new TopMenu
        {
            Title = "Arquivo",
            Items = [new MenuEntry { Title = "Sobre" }]
        },
        new TopMenu
        {
            Title = "Cadastros",
            Items =
            [
                new MenuEntry { Title = "Clientes" },
                new MenuEntry { Title = "Produtos" },
                new MenuEntry { Title = "Fornecedores" },
                new MenuEntry { Title = "Notas fiscais" }
            ]
        },
        new TopMenu
        {
            Title = "Movimentos",
            Items =
            [
                new MenuEntry { Title = "Vendas" },
                new MenuEntry { Title = "Consulta" },
                new MenuEntry { Title = "Histórico" }
            ]
        },
        new TopMenu
        {
            Title = "Relatórios",
            Items =
            [
                new MenuEntry { Title = "Vendas" },
                new MenuEntry { Title = "Financeiro" }
            ]
        },
        new TopMenu
        {
            Title = "Configurações",
            Items =
            [
                new MenuEntry { Title = "Parâmetros" },
                new MenuEntry { Title = "Usuários" }
            ]
        }
    ];

    private static IReadOnlyList<ShortcutItem> CreateShortcuts() =>
    [
        new ShortcutItem
        {
            Title = "Vendas",
            IsExpanded = true,
            Children =
            [
                new ShortcutItem { Title = "Consulta" },
                new ShortcutItem { Title = "Histórico" }
            ]
        },
        new ShortcutItem { Title = "Clientes" },
        new ShortcutItem { Title = "Produtos" },
        new ShortcutItem { Title = "Fornecedores" },
        new ShortcutItem { Title = "Notas fiscais" },
        new ShortcutItem { Title = "Relatórios" }
    ];
}
