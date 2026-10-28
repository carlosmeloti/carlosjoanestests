using AcaiPos.Api.Contratos;
using AcaiPos.Api.Persistence;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Api.Seguranca;

public sealed class InscricaoService
{
    private readonly CentralDbContext _db;
    private readonly ILogger<InscricaoService> _logger;

    public InscricaoService(CentralDbContext db, ILogger<InscricaoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InscricaoResponse> InscreverAsync(InscricaoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CodigoLoja)
            || string.IsNullOrWhiteSpace(request.NomeLoja)
            || string.IsNullOrWhiteSpace(request.CodigoTerminal)
            || string.IsNullOrWhiteSpace(request.NomeTerminal))
            throw new RecusaApiException("Loja e terminal são obrigatórios.");

        Store store;
        try
        {
            store = await ObterOuCriarLojaAsync(request, cancellationToken);
        }
        catch (DomainException ex)
        {
            throw new RecusaApiException(ex.Message);
        }

        var codigoTerminal = request.CodigoTerminal.Trim().ToUpperInvariant();
        var terminal = await _db.Terminals
            .FirstOrDefaultAsync(item => item.StoreId == store.Id && item.Code == codigoTerminal, cancellationToken);

        if (terminal is null)
        {
            try
            {
                terminal = Terminal.Create(store.Id, request.CodigoTerminal, request.NomeTerminal);
            }
            catch (DomainException ex)
            {
                throw new RecusaApiException(ex.Message);
            }

            _db.Add(terminal);
        }

        var hashAtual = _db.Entry(terminal).Property<string?>("SegredoHash").CurrentValue;
        if (!string.IsNullOrEmpty(hashAtual))
            throw new RecusaApiException("Segredo do terminal já foi emitido.", StatusCodes.Status409Conflict);

        var segredo = SegredoHasher.Gerar();
        _db.Entry(terminal).Property<string?>("SegredoHash").CurrentValue = SegredoHasher.Hash(segredo);
        _db.Entry(terminal).Property<DateTime?>("SegredoEmitidoEm").CurrentValue = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Terminal {TerminalId} da loja {StoreId} inscrito.", terminal.Id, store.Id);
        return new InscricaoResponse(store.Id, terminal.Id, segredo);
    }

    private async Task<Store> ObterOuCriarLojaAsync(InscricaoRequest request, CancellationToken cancellationToken)
    {
        var codigo = request.CodigoLoja.Trim().ToUpperInvariant();
        var store = await _db.Stores.FirstOrDefaultAsync(item => item.Code == codigo, cancellationToken);
        if (store is not null)
            return store;

        store = Store.Create(request.CodigoLoja, request.NomeLoja);
        _db.Add(store);
        return store;
    }
}

public sealed class RecusaApiException : Exception
{
    public RecusaApiException(string message, int statusCode = StatusCodes.Status400BadRequest) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
