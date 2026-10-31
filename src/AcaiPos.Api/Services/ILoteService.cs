using AcaiPos.Api.Dtos;

namespace AcaiPos.Api.Services;

public interface ILoteService
{
    Task<ResultadoLote> ReceberAsync(
        Guid terminalId,
        Guid storeId,
        LoteRequest lote,
        CancellationToken cancellationToken);
}
