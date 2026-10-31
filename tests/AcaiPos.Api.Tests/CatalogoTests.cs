using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AcaiPos.Api.Dtos;
using AcaiPos.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AcaiPos.Api.Tests;

public class CatalogoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CatalogoTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Primeiro_Terminal_Semeia_E_O_Segundo_So_Recebe()
    {
        var client = _factory.CreateClient();
        var primeiro = await InscreverAsync(client, "LOJA-CAT", "PDV-1");
        var segundo = await InscreverAsync(client, "LOJA-CAT", "PDV-2");
        var categoriaId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var formaId = Guid.NewGuid();

        Autenticar(client, primeiro.Token);
        var semeadura = await client.PostAsJsonAsync("/api/catalogo/semeadura", new
        {
            Categorias = new[]
            {
                new { Id = categoriaId, Name = "Bowls", IsActive = true }
            },
            Produtos = new[]
            {
                new
                {
                    Id = produtoId,
                    Name = "Açaí",
                    Sku = "ACAI",
                    CategoryIds = new[] { categoriaId },
                    SalePrice = 15m,
                    StockQuantity = 4m,
                    UnitOfMeasure = "UN",
                    IsActive = true,
                    Ncm = "20079990",
                    Cfop = "5102",
                    Csosn = "102"
                }
            },
            FormasPagamento = new[]
            {
                new { Id = formaId, Descricao = "PIX", CodigoTpag = "17", IsEnable = true }
            }
        });
        Assert.Equal(HttpStatusCode.OK, semeadura.StatusCode);

        var repetida = await client.PostAsJsonAsync("/api/catalogo/semeadura", new
        {
            Produtos = new[]
            {
                new
                {
                    Id = produtoId,
                    Name = "Nome do segundo terminal",
                    Sku = "OUTRO",
                    CategoryIds = new[] { categoriaId },
                    SalePrice = 99m,
                    UnitOfMeasure = "UN",
                    IsActive = true
                }
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);

        var produtoComToken = new HttpRequestMessage(HttpMethod.Post, "/api/catalogo/produtos")
        {
            Content = JsonContent.Create(new
            {
                Id = produtoId,
                LojaId = primeiro.LojaId,
                Name = "Alterado pelo terminal",
                Sku = "ACAI",
                CategoryIds = new[] { categoriaId },
                SalePrice = 15m,
                UnitOfMeasure = "UN",
                IsActive = true
            })
        };
        var recusa = await client.SendAsync(produtoComToken);
        Assert.Equal(HttpStatusCode.Unauthorized, recusa.StatusCode);

        Autenticar(client, segundo.Token);
        var recebido = await client.GetFromJsonAsync<CatalogoRemoto>("/api/catalogo");
        Assert.NotNull(recebido);
        Assert.Equal("Açaí", Assert.Single(recebido.Produtos).Name);
        Assert.Equal(produtoId, recebido.Produtos[0].Id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
        Assert.Equal(1, await db.Products.CountAsync(item => EF.Property<Guid>(item, "LojaId") == primeiro.LojaId));
    }

    [Fact]
    public async Task Preco_Mais_Recente_Vence_E_O_Antigo_Nao_Substitui()
    {
        var client = _factory.CreateClient();
        var acesso = await InscreverAsync(client, "LOJA-PRECO", "PDV-PRECO");
        var categoriaId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var antigo = DateTime.UtcNow.AddMinutes(-10);
        var recente = DateTime.UtcNow;

        using var categoria = new HttpRequestMessage(HttpMethod.Post, "/api/catalogo/categorias")
        {
            Content = JsonContent.Create(new { Id = categoriaId, LojaId = acesso.LojaId, Name = "Bowls", IsActive = true, UpdatedAt = antigo })
        };
        categoria.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(categoria)).StatusCode);

        using var produto = new HttpRequestMessage(HttpMethod.Post, "/api/catalogo/produtos")
        {
            Content = JsonContent.Create(new
            {
                Id = produtoId,
                LojaId = acesso.LojaId,
                Name = "Açaí",
                Sku = "ACAI-P",
                CategoryIds = new[] { categoriaId },
                SalePrice = 10m,
                StockQuantity = 3m,
                UnitOfMeasure = "UN",
                IsActive = true,
                Ncm = "20079990",
                Cfop = "5102",
                Csosn = "102",
                UpdatedAt = recente
            })
        };
        produto.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(produto)).StatusCode);

        Autenticar(client, acesso.Token);
        var atrasado = await client.PostAsJsonAsync("/api/catalogo/precos", new
        {
            Id = produtoId,
            SalePrice = 1m,
            UpdatedAt = antigo
        });
        var precoAntigo = await atrasado.Content.ReadFromJsonAsync<PrecoCatalogo>();
        Assert.Equal(HttpStatusCode.OK, atrasado.StatusCode);
        Assert.Equal(10m, precoAntigo!.SalePrice);

        var novoInstante = recente.AddMinutes(5);
        var atualizado = await client.PostAsJsonAsync("/api/catalogo/precos", new
        {
            Id = produtoId,
            SalePrice = 18m,
            UpdatedAt = novoInstante
        });
        var precoNovo = await atualizado.Content.ReadFromJsonAsync<PrecoCatalogo>();
        Assert.Equal(HttpStatusCode.OK, atualizado.StatusCode);
        Assert.Equal(18m, precoNovo!.SalePrice);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
        var gravado = await db.Products.SingleAsync(item => item.Id == produtoId);
        Assert.Equal(18m, gravado.SalePrice);
        Assert.Equal("Açaí", gravado.Name);
        Assert.Equal(3m, gravado.StockQuantity);
    }

    [Fact]
    public async Task Desde_Exclui_Produto_Antigo_E_Inclui_O_Recente()
    {
        var client = _factory.CreateClient();
        var acesso = await InscreverAsync(client, "LOJA-DESDE", "PDV-DESDE");
        var categoriaId = Guid.NewGuid();
        var antigoId = Guid.NewGuid();
        var recenteId = Guid.NewGuid();
        var antigo = DateTime.UtcNow.AddHours(-2);
        var recente = DateTime.UtcNow;
        var cursor = antigo.AddHours(1);

        using var categoria = new HttpRequestMessage(HttpMethod.Post, "/api/catalogo/categorias")
        {
            Content = JsonContent.Create(new { Id = categoriaId, LojaId = acesso.LojaId, Name = "Bowls", IsActive = true, UpdatedAt = antigo })
        };
        categoria.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(categoria)).StatusCode);

        using var produtoAntigo = new HttpRequestMessage(HttpMethod.Post, "/api/catalogo/produtos")
        {
            Content = JsonContent.Create(new
            {
                Id = antigoId,
                LojaId = acesso.LojaId,
                Name = "Antigo",
                Sku = "ANTIGO",
                CategoryIds = new[] { categoriaId },
                SalePrice = 10m,
                UnitOfMeasure = "UN",
                IsActive = true,
                Ncm = "20079990",
                Cfop = "5102",
                Csosn = "102",
                UpdatedAt = antigo
            })
        };
        produtoAntigo.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(produtoAntigo)).StatusCode);

        using var produtoRecente = new HttpRequestMessage(HttpMethod.Post, "/api/catalogo/produtos")
        {
            Content = JsonContent.Create(new
            {
                Id = recenteId,
                LojaId = acesso.LojaId,
                Name = "Recente",
                Sku = "RECENTE",
                CategoryIds = new[] { categoriaId },
                SalePrice = 12m,
                UnitOfMeasure = "UN",
                IsActive = true,
                Ncm = "20079990",
                Cfop = "5102",
                Csosn = "102",
                UpdatedAt = recente
            })
        };
        produtoRecente.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(produtoRecente)).StatusCode);

        Autenticar(client, acesso.Token);
        var recebido = await client.GetFromJsonAsync<CatalogoRemoto>(
            "/api/catalogo?desde=" + Uri.EscapeDataString(cursor.ToString("o")));
        Assert.NotNull(recebido);
        var produto = Assert.Single(recebido.Produtos);
        Assert.Equal(recenteId, produto.Id);
        Assert.Equal("Recente", produto.Name);
    }

    [Fact]
    public async Task Update_Sem_UpdatedAt_Entra_Na_Descida()
    {
        var client = _factory.CreateClient();
        var acesso = await InscreverAsync(client, "LOJA-TRG", "PDV-TRG");
        var categoriaId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var antigo = DateTime.UtcNow.AddHours(-2);
        var cursor = antigo.AddHours(1);

        using var categoria = new HttpRequestMessage(HttpMethod.Post, "/api/catalogo/categorias")
        {
            Content = JsonContent.Create(new { Id = categoriaId, LojaId = acesso.LojaId, Name = "Bowls", IsActive = true, UpdatedAt = antigo })
        };
        categoria.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(categoria)).StatusCode);

        using var produto = new HttpRequestMessage(HttpMethod.Post, "/api/catalogo/produtos")
        {
            Content = JsonContent.Create(new
            {
                Id = produtoId,
                LojaId = acesso.LojaId,
                Name = "Guaraná",
                Sku = "GUA-TRG",
                CategoryIds = new[] { categoriaId },
                SalePrice = 5m,
                UnitOfMeasure = "UN",
                IsActive = true,
                Ncm = "20079990",
                Cfop = "5102",
                Csosn = "102",
                UpdatedAt = antigo
            })
        };
        produto.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(produto)).StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
            var afetados = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "Products" SET "SalePrice" = {"8.75"} WHERE "Id" = {produtoId}""");
            Assert.Equal(1, afetados);
        }

        Autenticar(client, acesso.Token);
        var recebido = await client.GetFromJsonAsync<CatalogoRemoto>(
            "/api/catalogo?desde=" + Uri.EscapeDataString(cursor.ToString("o")));
        Assert.NotNull(recebido);
        var descido = Assert.Single(recebido.Produtos);
        Assert.Equal(produtoId, descido.Id);
        Assert.Equal(8.75m, descido.SalePrice);
    }

    private async Task<Acesso> InscreverAsync(HttpClient client, string loja, string terminal)
    {
        using var inscricao = new HttpRequestMessage(HttpMethod.Post, "/api/inscricoes")
        {
            Content = JsonContent.Create(new InscricaoRequest(loja, loja, terminal, terminal))
        };
        inscricao.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        var resposta = await client.SendAsync(inscricao);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var criada = await resposta.Content.ReadFromJsonAsync<InscricaoResponse>();
        Assert.NotNull(criada);

        var tokenResposta = await client.PostAsJsonAsync("/api/tokens", new TokenRequest(criada.TerminalId, criada.Segredo));
        var token = await tokenResposta.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        return new Acesso(criada.LojaId, token.AccessToken);
    }

    private static void Autenticar(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private sealed record Acesso(Guid LojaId, string Token);
}
