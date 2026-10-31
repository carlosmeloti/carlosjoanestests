using System.Text.Json;
using AcaiPos.Application.Sync;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Tests;

public class MontadorLoteTests
{
    [Fact]
    public void Sale_Payload_Uses_Central_Store_And_Terminal_And_Keeps_The_Local_Id()
    {
        var vendaId = Guid.NewGuid();
        var caixaId = Guid.NewGuid();
        var lojaLocal = Guid.NewGuid();
        var terminalLocal = Guid.NewGuid();
        var lojaCentral = Guid.NewGuid();
        var terminalCentral = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        var item = SaleItem.Restaurar(
            Guid.NewGuid(),
            vendaId,
            Guid.NewGuid(),
            "Açaí 500ml",
            "ACAI-500",
            1,
            15m,
            0,
            15m);
        var pagamento = Payment.Restaurar(
            Guid.NewGuid(),
            vendaId,
            FormaPagamento.IdDinheiro,
            "Dinheiro",
            15m,
            20m,
            5m,
            null,
            PaymentStatus.Confirmed,
            agora);
        var venda = Sale.Restaurar(
            vendaId,
            caixaId,
            terminalLocal,
            lojaLocal,
            Guid.NewGuid(),
            agora,
            15m,
            0,
            0,
            15m,
            SaleStatus.Completed,
            SaleOrigin.Pos,
            null,
            null,
            null,
            "0001",
            null,
            agora,
            agora,
            [item],
            [pagamento]);

        var corpo = MontadorLote.DeVenda(venda, [item], [pagamento], lojaCentral, terminalCentral);
        var json = JsonSerializer.Serialize(corpo, JsonLote.Opcoes);
        using var documento = JsonDocument.Parse(json);
        var enviada = documento.RootElement.GetProperty("vendas")[0];

        Assert.Equal(vendaId, enviada.GetProperty("id").GetGuid());
        Assert.Equal(caixaId, enviada.GetProperty("cashRegisterId").GetGuid());
        Assert.Equal(lojaCentral, enviada.GetProperty("storeId").GetGuid());
        Assert.Equal(terminalCentral, enviada.GetProperty("terminalId").GetGuid());
        Assert.NotEqual(lojaLocal, enviada.GetProperty("storeId").GetGuid());
        Assert.NotEqual(terminalLocal, enviada.GetProperty("terminalId").GetGuid());
        Assert.Equal(venda.StoreId, lojaLocal);
        Assert.Equal(venda.TerminalId, terminalLocal);
    }
}
