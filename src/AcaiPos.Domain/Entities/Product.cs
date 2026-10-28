using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class Product : AuditableEntity
{
    private readonly List<ProductCategory> _categories = [];

    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal? CostPrice { get; private set; }
    public decimal StockQuantity { get; private set; }
    public string UnitOfMeasure { get; private set; } = "UN";
    public string Ncm { get; private set; } = string.Empty;
    public string Cfop { get; private set; } = "5102";
    public int Origem { get; private set; }
    public string Csosn { get; private set; } = "102";
    public string? Cest { get; private set; }
    public string PisCst { get; private set; } = "49";
    public string CofinsCst { get; private set; } = "49";
    public string IbsCbsCst { get; private set; } = "000";
    public string CClassTrib { get; private set; } = "000001";
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<ProductCategory> Categories => _categories;
    public IReadOnlyCollection<Guid> CategoryIds => _categories.Select(link => link.CategoryId).ToList();

    private Product()
    {
    }

    public static Product Create(
        string name,
        string sku,
        IReadOnlyCollection<Guid> categoryIds,
        decimal salePrice,
        string? barcode = null,
        decimal? costPrice = null,
        decimal stockQuantity = 0,
        string unitOfMeasure = "UN",
        string? ncm = null,
        string cfop = "5102",
        int origem = 0,
        string csosn = "102",
        string? cest = null,
        string pisCst = "49",
        string cofinsCst = "49",
        string ibsCbsCst = "000",
        string cClassTrib = "000001")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do produto é obrigatório.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU é obrigatório.");
        if (salePrice < 0)
            throw new DomainException("Preço de venda não pode ser negativo.");
        if (stockQuantity < 0)
            throw new DomainException("Estoque não pode ser negativo.");

        var product = new Product
        {
            Name = name.Trim(),
            Sku = sku.Trim().ToUpperInvariant(),
            Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim(),
            SalePrice = decimal.Round(salePrice, 2),
            CostPrice = costPrice.HasValue ? decimal.Round(costPrice.Value, 2) : null,
            StockQuantity = stockQuantity,
            UnitOfMeasure = string.IsNullOrWhiteSpace(unitOfMeasure) ? "UN" : unitOfMeasure.Trim().ToUpperInvariant(),
            IsActive = true
        };

        product.DefinirDadosFiscais(ncm, cfop, origem, csosn, cest, pisCst, cofinsCst, ibsCbsCst, cClassTrib);
        product.SetCategories(categoryIds);
        return product;
    }

    public void Update(
        string name,
        string sku,
        IReadOnlyCollection<Guid> categoryIds,
        decimal salePrice,
        string? barcode,
        decimal? costPrice,
        string unitOfMeasure,
        bool isActive,
        string? ncm = null,
        string? cfop = null,
        int? origem = null,
        string? csosn = null,
        string? cest = null,
        string? pisCst = null,
        string? cofinsCst = null,
        string? ibsCbsCst = null,
        string? cClassTrib = null,
        bool atualizarDadosFiscais = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do produto é obrigatório.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU é obrigatório.");
        if (salePrice < 0)
            throw new DomainException("Preço de venda não pode ser negativo.");

        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        SalePrice = decimal.Round(salePrice, 2);
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        CostPrice = costPrice.HasValue ? decimal.Round(costPrice.Value, 2) : null;
        UnitOfMeasure = string.IsNullOrWhiteSpace(unitOfMeasure) ? "UN" : unitOfMeasure.Trim().ToUpperInvariant();
        IsActive = isActive;
        if (atualizarDadosFiscais)
            DefinirDadosFiscais(ncm, cfop, origem ?? Origem, csosn, cest, pisCst, cofinsCst, ibsCbsCst, cClassTrib);
        SetCategories(categoryIds);
        MarkUpdated();
    }

    public void DefinirDadosFiscais(
        string? ncm,
        string? cfop,
        int origem,
        string? csosn,
        string? cest,
        string? pisCst,
        string? cofinsCst,
        string? ibsCbsCst = null,
        string? cClassTrib = null)
    {
        var ncmLimpo = Digitos(ncm);
        if (ncmLimpo.Length > 0 && ncmLimpo.Length != 8)
            throw new DomainException("NCM deve ter 8 dígitos.");

        var cfopLimpo = Digitos(string.IsNullOrWhiteSpace(cfop) ? "5102" : cfop);
        if (cfopLimpo.Length != 4)
            throw new DomainException("CFOP deve ter 4 dígitos.");

        if (origem is < 0 or > 8)
            throw new DomainException("Origem da mercadoria deve estar entre 0 e 8.");

        var csosnLimpo = Digitos(string.IsNullOrWhiteSpace(csosn) ? "102" : csosn);
        if (csosnLimpo.Length != 3)
            throw new DomainException("CSOSN deve ter 3 dígitos.");

        var cestLimpo = Digitos(cest);
        if (cestLimpo.Length > 0 && cestLimpo.Length != 7)
            throw new DomainException("CEST deve ter 7 dígitos.");

        Ncm = ncmLimpo;
        Cfop = cfopLimpo;
        Origem = origem;
        Csosn = csosnLimpo;
        Cest = cestLimpo.Length == 0 ? null : cestLimpo;
        PisCst = NormalizarCst(pisCst, "49");
        CofinsCst = NormalizarCst(cofinsCst, "49");
        IbsCbsCst = NormalizarCodigo(ibsCbsCst, "000", 3, "CST do IBS/CBS");
        CClassTrib = NormalizarCodigo(cClassTrib, "000001", 6, "cClassTrib");
    }

    private static string NormalizarCodigo(string? valor, string padrao, int tamanho, string nome)
    {
        if (string.IsNullOrWhiteSpace(valor))
            valor = padrao;

        var digitos = Digitos(valor);
        if (digitos.Length != tamanho)
            throw new DomainException($"{nome} deve ter {tamanho} dígitos.");

        return digitos;
    }

    private static string NormalizarCst(string? valor, string padrao)
    {
        var digitos = Digitos(string.IsNullOrWhiteSpace(valor) ? padrao : valor);
        if (digitos.Length == 1)
            digitos = "0" + digitos;
        if (digitos.Length != 2)
            throw new DomainException("CST de PIS/COFINS deve ter 2 dígitos.");

        return digitos;
    }

    private static string Digitos(string? texto)
        => new((texto ?? string.Empty).Where(char.IsDigit).ToArray());

    public void SetCategories(IEnumerable<Guid> categoryIds)
    {
        var ids = categoryIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            throw new DomainException("Selecione ao menos uma categoria.");

        foreach (var link in _categories.Where(link => !ids.Contains(link.CategoryId)).ToList())
            _categories.Remove(link);

        foreach (var id in ids)
        {
            if (_categories.All(link => link.CategoryId != id))
                _categories.Add(ProductCategory.Create(Id, id));
        }
    }

    public void AdjustStock(decimal quantityDelta)
    {
        var newQuantity = StockQuantity + quantityDelta;
        if (newQuantity < 0)
            throw new DomainException($"Estoque insuficiente para {Name}.");

        StockQuantity = newQuantity;
        MarkUpdated();
    }
}
