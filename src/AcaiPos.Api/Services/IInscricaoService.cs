using AcaiPos.Api.Dtos;

namespace AcaiPos.Api.Services;

public interface IInscricaoService
{
    Task<InscricaoResponse> InscreverAsync(InscricaoRequest request, CancellationToken cancellationToken);
}
