using AcaiPos.Api.Dtos;

namespace AcaiPos.Api.Services;

public interface ITokenService
{
    Task<TokenResponse?> EmitirAsync(TokenRequest request, CancellationToken cancellationToken);
}
