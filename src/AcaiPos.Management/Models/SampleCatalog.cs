using System.Globalization;

namespace AcaiPos.Management.Models;

public static class SampleCatalog
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly string[] FirstNames =
    [
        "Ana", "Bruno", "Carla", "Diego", "Eduarda", "Fernanda", "Gabriel", "Helena",
        "Igor", "João", "Larissa", "Marcos", "Natália", "Otávio", "Patrícia", "Rafael"
    ];

    private static readonly string[] LastNames =
    [
        "Souza", "Lima", "Almeida", "Martins", "Oliveira", "Rocha", "Santos", "Ferreira",
        "Carvalho", "Gomes", "Ribeiro", "Barbosa", "Araújo", "Pereira", "Cardoso", "Mendes"
    ];

    private static readonly string[] Cities =
    [
        "São Paulo", "Rio de Janeiro", "Belo Horizonte", "Curitiba",
        "Porto Alegre", "Salvador", "Recife", "Fortaleza"
    ];

    private static readonly string[] Products =
    [
        "Açaí 300 ml", "Açaí 500 ml", "Açaí 700 ml", "Açaí 1 litro",
        "Granola", "Leite em pó", "Banana", "Morango",
        "Leite condensado", "Paçoca", "Nutella", "Mel",
        "Castanha", "Coco ralado", "Chantilly", "Cupuaçu 300 ml",
        "Cupuaçu 500 ml", "Suco de laranja", "Água sem gás", "Água com gás",
        "Refrigerante lata", "Brownie", "Tapioca", "Queijo coalho",
        "Calda de chocolate", "Calda de morango", "Leite ninho", "Ovomaltine"
    ];

    private static readonly string[] Categories =
    [
        "Tigela", "Acompanhamento", "Bebida", "Sobremesa"
    ];

    private static readonly string[] Units = ["un", "kg", "L", "pct"];

    public static IReadOnlyList<EntitySchema> Schemas { get; } = Create();

    private static IReadOnlyList<EntitySchema> Create()
    {
        var people = BuildPeople(47);
        var suppliers = BuildPeople(24);

        return
        [
            new EntitySchema
            {
                Entity = SearchEntity.Cliente,
                Label = "Cliente",
                SupportsDocument = true,
                Headers = ["Id", "Nome", "CPF", "Telefone", "Cidade"],
                Entries = people.Select((person, index) => new CatalogEntry
                {
                    Id = person.Id,
                    Name = person.Name,
                    Document = person.Document,
                    Cells = [person.Id, person.Name, person.Document, Phone(index), Cities[index % Cities.Length]]
                }).ToList()
            },
            new EntitySchema
            {
                Entity = SearchEntity.Produto,
                Label = "Produto",
                SupportsDocument = false,
                Headers = ["Id", "Nome", "Categoria", "Preço", "Unidade"],
                Entries = Products.Select((name, index) =>
                {
                    var id = (2001 + index).ToString(PtBr);
                    var price = (8.5m + (index * 1.75m)).ToString("C", PtBr);
                    return new CatalogEntry
                    {
                        Id = id,
                        Name = name,
                        Cells = [id, name, Categories[index % Categories.Length], price, Units[index % Units.Length]]
                    };
                }).ToList()
            },
            new EntitySchema
            {
                Entity = SearchEntity.Fornecedor,
                Label = "Fornecedor",
                SupportsDocument = true,
                Headers = ["Id", "Nome", "CPF/CNPJ", "Telefone", "Cidade"],
                Entries = suppliers.Select((person, index) => new CatalogEntry
                {
                    Id = (3001 + index).ToString(PtBr),
                    Name = person.Name,
                    Document = index % 3 == 0 ? Cnpj(index) : person.Document,
                    Cells =
                    [
                        (3001 + index).ToString(PtBr),
                        person.Name,
                        index % 3 == 0 ? Cnpj(index) : person.Document,
                        Phone(index + 3),
                        Cities[(index + 2) % Cities.Length]
                    ]
                }).ToList()
            },
            new EntitySchema
            {
                Entity = SearchEntity.NotaFiscal,
                Label = "Nota fiscal",
                SupportsDocument = false,
                Headers = ["Id", "Número", "Cliente", "Emissão", "Valor"],
                Entries = Enumerable.Range(0, 22).Select(index =>
                {
                    var id = (4001 + index).ToString(PtBr);
                    var number = $"NF-2026-{(index + 1):0000}";
                    var client = people[index].Name;
                    var issued = new DateTime(2026, 10, 13).AddDays(-index).ToString("dd/MM/yyyy", PtBr);
                    var amount = (45m + (index * 17.3m)).ToString("C", PtBr);
                    return new CatalogEntry
                    {
                        Id = id,
                        Name = client,
                        Cells = [id, number, client, issued, amount]
                    };
                }).ToList()
            },
            new EntitySchema
            {
                Entity = SearchEntity.Venda,
                Label = "Venda",
                SupportsDocument = false,
                Headers = ["Id", "Data", "Cliente", "Itens", "Total"],
                Entries = Enumerable.Range(0, 36).Select(index =>
                {
                    var id = (5001 + index).ToString(PtBr);
                    var when = new DateTime(2026, 10, 13).AddDays(-index).ToString("dd/MM/yyyy", PtBr);
                    var client = people[index % people.Count].Name;
                    var items = (1 + (index % 6)).ToString(PtBr);
                    var total = (18m + (index * 9.4m)).ToString("C", PtBr);
                    return new CatalogEntry
                    {
                        Id = id,
                        Name = client,
                        Cells = [id, when, client, items, total]
                    };
                }).ToList()
            }
        ];
    }

    private static List<(string Id, string Name, string Document)> BuildPeople(int count)
    {
        var people = new List<(string Id, string Name, string Document)>(count);
        for (var index = 0; index < count; index++)
        {
            var name = $"{FirstNames[index % FirstNames.Length]} {LastNames[(index * 3) % LastNames.Length]}";
            people.Add(((1001 + index).ToString(PtBr), name, Cpf(index)));
        }

        return people;
    }

    private static string Cpf(int index)
    {
        var digits = (10000000000L + (index * 137258369L)).ToString("00000000000", CultureInfo.InvariantCulture);
        return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}";
    }

    private static string Cnpj(int index)
    {
        var digits = (10000000000000L + (index * 975318642L)).ToString("00000000000000", CultureInfo.InvariantCulture);
        return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}";
    }

    private static string Phone(int index)
    {
        var area = 11 + (index % 8);
        var suffix = 1000 + (index * 37 % 9000);
        return $"({area}) 9{8000 + (index % 1000):0000}-{suffix:0000}";
    }
}
