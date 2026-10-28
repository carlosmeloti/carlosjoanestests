using System.Globalization;
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
            0m,
            50m,
            10m,
            [
                new TicketItemDto("Açaí 500ml", "ACAI-500", 2, 25m, 0m, 50m)
            ],
            [
                new TicketPaymentDto("Dinheiro", 50m, 60m, 10m)
            ],
            IsReprint: true);

        var text = new TextTicketFormatter().Format(ticket);

        Assert.Contains("T20261005-0001", text);
        Assert.Contains("Açaí 500ml", text);
        Assert.Contains("REIMPRESSAO", text);
        Assert.Contains("Troco", text);
        Assert.DoesNotContain("12.741", text);
        Assert.DoesNotContain("IBS", text);
    }

    [Fact]
    public void Format_Nfce_ShowsIncidentTaxesAndFederalLaw()
    {
        var ticket = Cupom(temCupomFiscal: true, ibs: 0.01m, cbs: 0.09m);

        var text = new TextTicketFormatter().Format(ticket);

        Assert.Contains("DANFE NFC-e", text);
        Assert.Contains("Tributos Totais Incidentes", text);
        Assert.Contains("(Lei Federal 12.741/2012)", text);
        Assert.Contains("IBS", text);
        Assert.Contains("CBS", text);
        Assert.Contains("0,10", text);
        foreach (var line in text.Split('\n'))
            Assert.True(line.TrimEnd('\r').Length <= 42, line);
    }

    [Fact]
    public void Format_Nfce_WithoutTaxes_StillShowsFederalLaw()
    {
        var text = new TextTicketFormatter().Format(Cupom(temCupomFiscal: true, ibs: null, cbs: null));

        Assert.Contains("Lei Federal 12.741/2012", text);
        Assert.DoesNotContain("IBS", text);
        Assert.DoesNotContain("CBS", text);
    }

    private static TicketDto Cupom(bool temCupomFiscal, decimal? ibs, decimal? cbs) => new(
        Guid.NewGuid(),
        "T20261026-0002",
        "Açaí Prime Centro",
        "LOJA-01",
        "Açaí Prime Centro",
        "PDV-01",
        "Operador",
        DateTime.UtcNow,
        SaleStatus.Completed,
        10m,
        0m,
        0m,
        10m,
        0m,
        [new TicketItemDto("Açaí 500ml", "ACAI-500", 1, 10m, 0m, 10m)],
        [new TicketPaymentDto("Dinheiro", 10m, 10m, 0m)],
        IsReprint: false,
        TemCupomFiscal: temCupomFiscal,
        TributoIbs: ibs,
        TributoCbs: cbs);
}

public class CashMovementTextFormatterTests
{
    private static readonly DateTime OpenedAt = new(2026, 10, 18, 8, 12, 0);
    private static readonly DateTime ClosedAt = new(2026, 10, 18, 22, 40, 0);
    private static readonly DateTime PrintedAt = new(2026, 10, 18, 23, 44, 0);

    [Fact]
    public void Format_ClosedSession_IncludesClosingDateTime()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        try
        {
            var text = new CashMovementTextFormatter().Format(Report(isOpen: false, ClosedAt));
            var flat = Flatten(text);

            Assert.Contains("FECHADO", text);
            Assert.Contains("18/10/2026 08:12", text);
            Assert.Contains("18/10/2026 22:40", text);
            Assert.DoesNotContain("em aberto", text);
            Assert.Contains("Troco banco", text);
            Assert.Contains("Reforço de troco", text);
            Assert.Contains("Dinheiro", text);
            Assert.Contains("Impresso em 18/10/2026 23:44", text);
            Assert.Contains(CashMovementTextFormatter.CashBalanceFormula, flat);
            Assert.Contains(CashMovementTextFormatter.GeneralBalanceFormula, flat);
            AssertLinesFit(text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Format_OpenSession_ShowsClosingAsStillOpen()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        try
        {
            var detail = Session(
                isOpen: true,
                closedAt: null,
                withdrawals: [],
                supplies: []);
            var text = new CashMovementTextFormatter().Format(new CashMovementReport("LOJA-01", "Açaí da Praça", detail, PrintedAt));

            Assert.Contains("ABERTO", text);
            Assert.Contains("18/10/2026 08:12", text);
            Assert.Contains("em aberto", text);
            Assert.DoesNotContain("22:40", text);
            Assert.Contains(CashMovementTextFormatter.NoWithdrawalsText, text);
            Assert.Contains(CashMovementTextFormatter.NoSuppliesText, text);
            AssertLinesFit(text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static CashMovementReport Report(bool isOpen, DateTime? closedAt)
        => new("LOJA-01", "Açaí da Praça", Session(isOpen, closedAt, null, null), PrintedAt);

    private static CashSessionDetailDto Session(
        bool isOpen,
        DateTime? closedAt,
        IReadOnlyList<CashSessionMovementDto>? withdrawals,
        IReadOnlyList<CashSessionMovementDto>? supplies)
    {
        withdrawals ??=
        [
            new CashSessionMovementDto(Guid.NewGuid(), 100m, "Troco banco", new DateTime(2026, 10, 18, 10, 30, 0), "Maria Souza")
        ];
        supplies ??=
        [
            new CashSessionMovementDto(Guid.NewGuid(), 50m, "Reforço de troco", new DateTime(2026, 10, 18, 9, 0, 0), "Maria Souza")
        ];

        return new CashSessionDetailDto(
            Guid.NewGuid(),
            OpenedAt,
            closedAt,
            isOpen,
            "Maria Souza",
            "CX-01",
            200m,
            2980m,
            50m,
            180m,
            1450m,
            3050m,
            [new PaymentTotalDto("Dinheiro", 1380m)],
            withdrawals,
            supplies);
    }

    private static string Flatten(string text)
        => string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()));

    private static void AssertLinesFit(string text)
    {
        foreach (var line in text.Split('\n'))
            Assert.True(line.TrimEnd('\r').Length <= CashMovementTextFormatter.Width, line);
    }
}
