using AcaiPos.Application.Models;
using AcaiPos.Application.Tickets;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Tests;

public class TicketFormatterTests
{
    [Fact]
    public void Format_Includes_Items_Payments_And_Reprint_Marker()
    {
        var ticket = new TicketDto(
            Guid.NewGuid(),
            "T20261005-0001",
            "Açaí Prime Centro",
            "LOJA-01",
            "Açaí Prime Centro",
            "PDV-01",
            "Operador",
            DateTime.UtcNow,
            SaleStatus.Completed,
            50m,
            0m,
            50m,
            10m,
            [
                new TicketItemDto("Açaí 500ml", "ACAI-500", 2, 25m, 0m, 50m)
            ],
            [
                new TicketPaymentDto(PaymentMethod.Cash, "Dinheiro", 50m, 60m, 10m)
            ],
            IsReprint: true);

        var text = new TextTicketFormatter().Format(ticket);

        Assert.Contains("T20261005-0001", text);
        Assert.Contains("Açaí 500ml", text);
        Assert.Contains("REIMPRESSAO", text);
        Assert.Contains("Troco", text);
    }
}
