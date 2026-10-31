using AcaiPos.Api.Dtos;
using AcaiPos.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcaiPos.Api.Controllers;

[ApiController]
[Route("api/tokens")]
[Tags("Token")]
[EnableRateLimiting("tokens")]
public sealed class TokensController : ControllerBase
{
    private readonly ITokenService _service;

    public TokensController(ITokenService service)
    {
        _service = service;
    }

    [HttpPost]
    [EndpointSummary("Troca o segredo do terminal por um token curto.")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Emitir(TokenRequest request, CancellationToken cancellationToken)
    {
        var token = await _service.EmitirAsync(request, cancellationToken);
        return token is null ? Unauthorized() : Ok(token);
    }
}
