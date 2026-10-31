using AcaiPos.Api.Dtos;
using AcaiPos.Api.Repositories;
using AcaiPos.Api.Seguranca;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;

namespace AcaiPos.Api.Services;

public sealed class InscricaoService : IInscricaoService
{
    private readonly ILojaRepository _lojas;
    private readonly ITerminalRepository _terminais;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InscricaoService> _logger;

    public InscricaoService(
        ILojaRepository lojas,
        ITerminalRepository terminais,
        IUnitOfWork unitOfWork,
        ILogger<InscricaoService> logger)
    {
        _lojas = lojas;
        _terminais = terminais;
        _unitOfWork = unitOfWork;
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
        var terminal = await _terminais.ObterPorLojaECodigoAsync(store.Id, codigoTerminal, cancellationToken);

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

            _terminais.Adicionar(terminal);
        }

        var hashAtual = _terminais.LerSegredoHash(terminal);
        if (!string.IsNullOrEmpty(hashAtual))
            throw new RecusaApiException("Segredo do terminal já foi emitido.", StatusCodes.Status409Conflict);

        var segredo = SegredoHasher.Gerar();
        _terminais.GravarSegredo(terminal, SegredoHasher.Hash(segredo), DateTime.UtcNow);
        await _unitOfWork.SalvarAsync(cancellationToken);

        _logger.LogInformation("Terminal {TerminalId} da loja {StoreId} inscrito.", terminal.Id, store.Id);
        return new InscricaoResponse(store.Id, terminal.Id, segredo);
    }

    private async Task<Store> ObterOuCriarLojaAsync(InscricaoRequest request, CancellationToken cancellationToken)
    {
        var codigo = request.CodigoLoja.Trim().ToUpperInvariant();
        var store = await _lojas.ObterPorCodigoAsync(codigo, cancellationToken);
        if (store is not null)
            return store;

        store = Store.Create(request.CodigoLoja, request.NomeLoja);
        _lojas.Adicionar(store);
        return store;
    }
}
