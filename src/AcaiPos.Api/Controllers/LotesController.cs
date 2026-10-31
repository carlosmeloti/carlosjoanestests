using AcaiPos.Api.Dtos;
using AcaiPos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcaiPos.Api.Controllers;

[ApiController]
[Route("api/lotes")]
[Tags("Lote")]
[Authorize]
public sealed class LotesController : ControllerBase
{
    private readonly ILoteService _service;

    public LotesController(ILoteService service)
    {
        _service = service;
    }

    [HttpPost]
    [EndpointSummary("Grava o lote da loja e do terminal do token. Repetir o mesmo id atualiza a linha.")]
    [ProducesResponseType<LoteAceito>(StatusCodes.Status200OK)]
    [ProducesResponseType<LoteRecusado>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receber(LoteRequest lote, CancellationToken cancellationToken)
    {
        var terminalId = Guid.Parse(User.FindFirst("sub")!.Value);
        var storeId = Guid.Parse(User.FindFirst("store_id")!.Value);
        var resultado = await _service.ReceberAsync(terminalId, storeId, lote, cancellationToken);
        return resultado switch
        {
            LoteAceito aceito => Ok(aceito),
            LoteRecusado recusado => BadRequest(recusado),
            _ => BadRequest()
        };
    }
}
