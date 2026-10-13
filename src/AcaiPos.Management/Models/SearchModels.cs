using System.Windows.Input;

namespace AcaiPos.Management.Models;

public enum SearchField
{
    Id,
    Name,
    Cpf
}

public enum SearchEntity
{
    Cliente,
    Produto,
    Fornecedor,
    NotaFiscal,
    Venda
}

public sealed class EntitySchema
{
    public required SearchEntity Entity { get; init; }
    public required string Label { get; init; }
    public required bool SupportsDocument { get; init; }
    public required IReadOnlyList<string> Headers { get; init; }
    public required IReadOnlyList<CatalogEntry> Entries { get; init; }

    public override string ToString() => Label;
}

public sealed class CatalogEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Document { get; init; }
    public required IReadOnlyList<string> Cells { get; init; }
}

public sealed class ResultRow
{
    public required string C0 { get; init; }
    public required string C1 { get; init; }
    public required string C2 { get; init; }
    public required string C3 { get; init; }
    public required string C4 { get; init; }
}

public sealed class PageNumber
{
    public int Number { get; init; }
    public bool IsCurrent { get; init; }
    public ICommand? SelectCommand { get; init; }
}

public sealed class MenuEntry
{
    public required string Title { get; init; }
}

public sealed class TopMenu
{
    public required string Title { get; init; }
    public required IReadOnlyList<MenuEntry> Items { get; init; }
}
