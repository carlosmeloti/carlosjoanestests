using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Services;

public sealed class TicketService
{
    private readonly IPosDbContext _db;
    private readonly ICurrentSession _session;
    private readonly ITicketFormatter _formatter;
    private readonly ITicketPrinter _printer;
    private readonly IAuditService _audit;

    public TicketService(
        IPosDbContext db,
        ICurrentSession session,
        ITicketFormatter formatter,
        ITicketPrinter printer,
        IAuditService audit)
    {
        _db = db;
        _session = session;
        _formatter = formatter;
        _printer = printer;
        _audit = audit;
    }

    public Task<TicketDto> GetTicketAsync(Guid saleId, bool isReprint = false, CancellationToken cancellationToken = default)
    {
        var sale = _db.Sales.FirstOrDefault(s => s.Id == saleId)
            ?? throw new DomainException("Venda não encontrada.");

        return Task.FromResult(BuildTicket(sale, isReprint));
    }

    public Task<TicketDto?> GetLastCompletedTicketAsync(CancellationToken cancellationToken = default)
    {
        if (_session.CurrentTerminal is null)
            return Task.FromResult<TicketDto?>(null);

        var sale = _db.Sales
            .Where(s => s.TerminalId == _session.CurrentTerminal.Id && s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.SoldAt)
            .FirstOrDefault();

        return Task.FromResult(sale is null ? null : BuildTicket(sale, isReprint: false));
    }

    public Task<IReadOnlyList<TicketDto>> GetTodayTicketsAsync(CancellationToken cancellationToken = default)
    {
        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(1);

        var query = _db.Sales.Where(s => s.SoldAt >= start && s.SoldAt < end);
        if (_session.CurrentTerminal is not null)
            query = query.Where(s => s.TerminalId == _session.CurrentTerminal.Id);

        var sales = query
            .OrderByDescending(s => s.SoldAt)
            .ToList();

        var tickets = sales.Select(s => BuildTicket(s, isReprint: false)).ToList();
        return Task.FromResult<IReadOnlyList<TicketDto>>(tickets);
    }

    public string Format(TicketDto ticket) => _formatter.Format(ticket);

    public async Task PrintAsync(Guid saleId, bool isReprint = true, CancellationToken cancellationToken = default)
    {
        if (!_session.IsAuthenticated)
            throw new DomainException("Sessão inválida.");

        var ticket = await GetTicketAsync(saleId, isReprint, cancellationToken);
        await _printer.PrintAsync(ticket, cancellationToken);

        await _audit.LogAsync(
            isReprint ? "TICKET_REPRINT" : "TICKET_PRINT",
            "Sale",
            saleId,
            $"Ticket {ticket.TicketNumber}",
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private TicketDto BuildTicket(Domain.Entities.Sale sale, bool isReprint)
    {
        var operatorName = _db.Users.FirstOrDefault(u => u.Id == sale.OperatorId)?.Name ?? "—";
        var terminal = _db.Terminals.FirstOrDefault(t => t.Id == sale.TerminalId);
        var store = terminal is null
            ? null
            : _db.Stores.FirstOrDefault(s => s.Id == terminal.StoreId);

        var items = _db.SaleItems.Where(i => i.SaleId == sale.Id).ToList();
        if (items.Count == 0 && sale.Items.Count > 0)
            items = sale.Items.ToList();

        var payments = _db.Payments.Where(p => p.SaleId == sale.Id).ToList();
        if (payments.Count == 0 && sale.Payments.Count > 0)
            payments = sale.Payments.ToList();

        return new TicketDto(
            sale.Id,
            sale.TicketNumber,
            store?.Name ?? "AcaiPos",
            store?.Code ?? "LOJA",
            store?.Name ?? "Loja",
            terminal?.Code ?? "PDV",
            operatorName,
            sale.SoldAt,
            sale.Status,
            sale.Subtotal,
            sale.DiscountAmount,
            sale.Total,
            sale.ChangeAmount,
            items.Select(i => new TicketItemDto(
                i.ProductName,
                i.ProductSku,
                i.Quantity,
                i.UnitPrice,
                i.DiscountAmount,
                i.Subtotal)).ToList(),
            payments
                .Where(p => p.Status != PaymentStatus.Cancelled)
                .Select(p => new TicketPaymentDto(
                    p.Method,
                    MethodLabel(p.Method),
                    p.Amount,
                    p.AmountReceived,
                    p.ChangeAmount)).ToList(),
            isReprint);
    }

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.DebitCard => "Cartão débito",
        PaymentMethod.CreditCard => "Cartão crédito",
        PaymentMethod.Pix => "PIX",
        _ => method.ToString()
    };
}
