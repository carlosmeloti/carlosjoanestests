using AcaiPos.Api.Dtos;
using AcaiPos.Api.Seguranca;
using AcaiPos.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcaiPos.Api.Controllers;

[ApiController]
[Route("api/inscricoes")]
[Tags("Inscrição")]
public sealed class InscricoesController : ControllerBase
{
    private readonly IInscricaoService _service;
    private readonly IConfiguration _configuration;

    public InscricoesController(IInscricaoService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    [HttpPost]
    [EndpointSummary("Inscreve a loja e o terminal e devolve o segredo uma vez.")]
    [ProducesResponseType<InscricaoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inscrever(
        InscricaoRequest request,
        [FromHeader(Name = "X-Chave-Admin")] string? chaveAdmin,
        CancellationToken cancellationToken)
    {
        var esperada = _configuration["Sync:ChaveAdmin"] ?? string.Empty;
        if (string.IsNullOrEmpty(chaveAdmin) || !SegredoHasher.Iguais(chaveAdmin, esperada))
            return Unauthorized();

        try
        {
            var resposta = await _service.InscreverAsync(request, cancellationToken);
            return Ok(resposta);
        }
        catch (RecusaApiException ex)
        {
            return StatusCode(ex.StatusCode, new { mensagem = ex.Message });
        }
    }
}
