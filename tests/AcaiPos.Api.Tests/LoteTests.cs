using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AcaiPos.Api.Dtos;
using AcaiPos.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AcaiPos.Api.Tests;

public class LoteTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LoteTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Mesmo_Lote_Enviado_Duas_Vezes_Deixa_Uma_Linha_De_Cada_Agregado()
    {
        var client = _factory.CreateClient();
        var acesso = await InscreverETokenAsync(client, "LOJA-A", "Loja A", "PDV-A", "Terminal A");
        var vendaId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var pagamentoId = Guid.NewGuid();
        var caixaId = Guid.NewGuid();
        var movimentoCaixaId = Guid.NewGuid();
        var estoqueId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var operadorId = Guid.NewGuid();

        var primeiro = Lote(
            acesso,
            vendaId,
            itemId,
            pagamentoId,
            caixaId,
            movimentoCaixaId,
            estoqueId,
            produtoId,
            operadorId,
            total: 10m);
        var segunda = Lote(
            acesso,
            vendaId,
            itemId,
            pagamentoId,
            caixaId,
            movimentoCaixaId,
            estoqueId,
            produtoId,
            operadorId,
            total: 12m);

        Autenticar(client, acesso.Token);
        var primeiraResposta = await client.PostAsJsonAsync("/api/lotes", primeiro);
        var segundaResposta = await client.PostAsJsonAsync("/api/lotes", segunda);

        Assert.Equal(HttpStatusCode.OK, primeiraResposta.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segundaResposta.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
        Assert.Equal(1, await db.Sales.CountAsync());
        Assert.Equal(1, await db.SaleItems.CountAsync());
        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.CashRegisters.CountAsync());
        Assert.Equal(1, await db.CashMovements.CountAsync());
        Assert.Equal(1, await db.StockMovements.CountAsync());
        Assert.Equal(12m, (await db.Sales.SingleAsync()).Total);
    }

    [Fact]
    public async Task Lote_Sem_Token_De_Outra_Loja_Ou_Sem_Item_E_Recusado()
    {
        var client = _factory.CreateClient();
        var lojaA = await InscreverETokenAsync(client, "LOJA-B", "Loja B", "PDV-B", "Terminal B");
        var lojaB = await InscreverETokenAsync(client, "LOJA-C", "Loja C", "PDV-C", "Terminal C");
        var operadorId = Guid.NewGuid();

        var vendaSemToken = Guid.NewGuid();
        var semToken = new
        {
            Vendas = new[] { Venda(lojaA, vendaSemToken, operadorId, 10m, itens: true) }
        };
        var respostaSemToken = await client.PostAsJsonAsync("/api/lotes", semToken);
        Assert.Equal(HttpStatusCode.Unauthorized, respostaSemToken.StatusCode);

        Autenticar(client, lojaA.Token);
        var caixaRecusada = Guid.NewGuid();
        var vendaSemItem = Guid.NewGuid();
        var semItem = new
        {
            vendas = new[]
            {
                Venda(lojaA, vendaSemItem, operadorId, 10m, itens: false)
            },
            caixas = new[]
            {
                Caixa(lojaA, caixaRecusada, operadorId)
            }
        };
        var respostaSemItem = await client.PostAsJsonAsync("/api/lotes", semItem);
        Assert.Equal(HttpStatusCode.BadRequest, respostaSemItem.StatusCode);

        var vendaOutraLoja = Guid.NewGuid();
        var corpoOutraLoja = new
        {
            Vendas = new[] { Venda(lojaB, vendaOutraLoja, operadorId, 10m, itens: true) }
        };
        var respostaOutraLoja = await client.PostAsJsonAsync("/api/lotes", corpoOutraLoja);
        Assert.Equal(HttpStatusCode.BadRequest, respostaOutraLoja.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
        Assert.False(await db.Sales.AnyAsync(item => item.Id == vendaSemToken));
        Assert.False(await db.Sales.AnyAsync(item => item.Id == vendaSemItem));
        Assert.False(await db.CashRegisters.AnyAsync(item => item.Id == caixaRecusada));
        Assert.False(await db.Sales.AnyAsync(item => item.Id == vendaOutraLoja));
    }

    private async Task<Acesso> InscreverETokenAsync(
        HttpClient client,
        string codigoLoja,
        string nomeLoja,
        string codigoTerminal,
        string nomeTerminal)
    {
        using var inscricao = new HttpRequestMessage(HttpMethod.Post, "/api/inscricoes")
        {
            Content = JsonContent.Create(new InscricaoRequest(codigoLoja, nomeLoja, codigoTerminal, nomeTerminal))
        };
        inscricao.Headers.Add("X-Chave-Admin", "chave-admin-teste");
        var respostaInscricao = await client.SendAsync(inscricao);
        Assert.Equal(HttpStatusCode.OK, respostaInscricao.StatusCode);
        var criada = await respostaInscricao.Content.ReadFromJsonAsync<InscricaoResponse>();
        Assert.NotNull(criada);
        Assert.False(string.IsNullOrWhiteSpace(criada.Segredo));

        var respostaToken = await client.PostAsJsonAsync(
            "/api/tokens",
            new TokenRequest(criada.TerminalId, criada.Segredo));
        Assert.Equal(HttpStatusCode.OK, respostaToken.StatusCode);
        var token = await respostaToken.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);

        return new Acesso(criada.LojaId, criada.TerminalId, token.AccessToken);
    }

    private static void Autenticar(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static object Lote(
        Acesso acesso,
        Guid vendaId,
        Guid itemId,
        Guid pagamentoId,
        Guid caixaId,
        Guid movimentoCaixaId,
        Guid estoqueId,
        Guid produtoId,
        Guid operadorId,
        decimal total)
    {
        return new
        {
            Vendas = new[]
            {
                new
                {
                    Id = vendaId,
                    CashRegisterId = caixaId,
                    TerminalId = acesso.TerminalId,
                    StoreId = acesso.LojaId,
                    OperatorId = operadorId,
                    Status = "Completed",
                    Origin = "Pos",
                    TicketNumber = $"T-{vendaId:N}"[..12],
                    Subtotal = total,
                    Total = total,
                    Itens = new[]
                    {
                        new
                        {
                            Id = itemId,
                            ProductId = produtoId,
                            ProductName = "Açaí",
                            ProductSku = "ACAI",
                            Quantity = 1m,
                            UnitPrice = total,
                            Subtotal = total
                        }
                    },
                    Pagamentos = new[]
                    {
                        new
                        {
                            Id = pagamentoId,
                            FormaPagamentoId = Guid.Parse("00000000-0000-4000-8000-000000000001"),
                            Descricao = "PIX",
                            Amount = total,
                            Status = "Confirmed"
                        }
                    }
                }
            },
            Caixas = new[]
            {
                Caixa(acesso, caixaId, operadorId)
            },
            MovimentacoesCaixa = new[]
            {
                new
                {
                    Id = movimentoCaixaId,
                    CashRegisterId = caixaId,
                    TerminalId = acesso.TerminalId,
                    OperatorId = operadorId,
                    Type = "Supply",
                    Amount = 5m,
                    Reason = "Troco"
                }
            },
            MovimentacoesEstoque = new[]
            {
                new
                {
                    Id = estoqueId,
                    ProductId = produtoId,
                    SaleId = vendaId,
                    OperatorId = operadorId,
                    Type = "Sale",
                    Quantity = -1m,
                    PreviousQuantity = 10m,
                    ResultingQuantity = 9m,
                    Reason = "Venda"
                }
            }
        };
    }

    private static object Venda(Acesso acesso, Guid vendaId, Guid operadorId, decimal total, bool itens)
    {
        return new
        {
            Id = vendaId,
            CashRegisterId = Guid.NewGuid(),
            TerminalId = acesso.TerminalId,
            StoreId = acesso.LojaId,
            OperatorId = operadorId,
            Status = "Completed",
            Origin = "Pos",
            TicketNumber = $"T-{vendaId:N}"[..12],
            Subtotal = total,
            Total = total,
            Itens = itens
                ? new[]
                {
                    new
                    {
                        Id = Guid.NewGuid(),
                        ProductId = Guid.NewGuid(),
                        ProductName = "Açaí",
                        ProductSku = "ACAI",
                        Quantity = 1m,
                        UnitPrice = total,
                        Subtotal = total
                    }
                }
                : Array.Empty<object>(),
            Pagamentos = Array.Empty<object>()
        };
    }

    private static object Caixa(Acesso acesso, Guid caixaId, Guid operadorId)
    {
        return new
        {
            Id = caixaId,
            TerminalId = acesso.TerminalId,
            StoreId = acesso.LojaId,
            OperatorId = operadorId,
            OpeningAmount = 100m,
            Status = "Open"
        };
    }

    private sealed record Acesso(Guid LojaId, Guid TerminalId, string Token);
}
