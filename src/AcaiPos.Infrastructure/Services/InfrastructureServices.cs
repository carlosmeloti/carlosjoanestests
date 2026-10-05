using AcaiPos.Application.Abstractions;
using AcaiPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class TicketNumberGenerator : ITicketNumberGenerator
{
    private readonly PosDbContext _db;
    private readonly object _gate = new();

    public TicketNumberGenerator(PosDbContext db)
    {
        _db = db;
    }

    public Task<string> NextAsync(Guid terminalId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"T{today}-";
            var last = _db.Sales
                .Where(s => s.TerminalId == terminalId && s.TicketNumber.StartsWith(prefix))
                .OrderByDescending(s => s.TicketNumber)
                .Select(s => s.TicketNumber)
                .FirstOrDefault();

            var sequence = 1;
            if (!string.IsNullOrWhiteSpace(last))
            {
                var parts = last.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out var n))
                    sequence = n + 1;
            }

            return Task.FromResult($"{prefix}{sequence:D4}");
        }
    }
}
