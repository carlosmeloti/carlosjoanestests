namespace AcaiPos.Application.Sync;

public sealed class CategoriaRemota
{
    public Guid Id { get; set; }
    public Guid LojaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class ProdutoRemoto
{
    public Guid Id { get; set; }
    public Guid LojaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
    public decimal SalePrice { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal StockQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = "UN";
    public bool IsActive { get; set; } = true;
    public string Ncm { get; set; } = string.Empty;
    public string Cfop { get; set; } = "5102";
    public int Origem { get; set; }
    public string Csosn { get; set; } = "102";
    public string? Cest { get; set; }
    public string PisCst { get; set; } = "49";
    public string CofinsCst { get; set; } = "49";
    public string IbsCbsCst { get; set; } = "000";
    public string CClassTrib { get; set; } = "000001";
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class FormaRemota
{
    public Guid Id { get; set; }
    public Guid LojaId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool CalculaTroco { get; set; }
    public bool IsParcela { get; set; }
    public string CorRgbBotao { get; set; } = "#2A3642";
    public int Ordem { get; set; }
    public bool IsEnable { get; set; } = true;
    public string CodigoTpag { get; set; } = "99";
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class SemeaduraRemota
{
    public List<CategoriaRemota> Categorias { get; set; } = [];
    public List<ProdutoRemoto> Produtos { get; set; } = [];
    public List<FormaRemota> FormasPagamento { get; set; } = [];
}

public sealed class CatalogoRecebido
{
    public List<CategoriaRemota> Categorias { get; set; } = [];
    public List<ProdutoRemoto> Produtos { get; set; } = [];
    public List<FormaRemota> FormasPagamento { get; set; } = [];
}

public sealed class PrecoRemoto
{
    public Guid Id { get; set; }
    public decimal SalePrice { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum TipoSemeadura
{
    Aceita,
    JaExiste,
    Falha
}

public sealed record ResultadoSemeadura(TipoSemeadura Tipo, string? Mensagem);

public sealed record ResultadoPreco(bool Aceito, PrecoRemoto? Preco, int? HttpStatus, string? Mensagem);
