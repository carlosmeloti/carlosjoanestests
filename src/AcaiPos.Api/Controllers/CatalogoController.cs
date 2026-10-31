using AcaiPos.Api.Dtos;
using AcaiPos.Api.Seguranca;
using AcaiPos.Api.Services;
using AcaiPos.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcaiPos.Api.Controllers;

[ApiController]
[Route("api/catalogo")]
[Tags("Catálogo")]
public sealed class CatalogoController : ControllerBase
{
    private readonly ICatalogoService _service;
    private readonly IConfiguration _configuration;

    public CatalogoController(ICatalogoService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    [HttpPost("categorias")]
    [EndpointSummary("Grava ou atualiza uma categoria da loja.")]
    public Task<IActionResult> Categoria(
        CategoriaCatalogo pedido,
        [FromHeader(Name = "X-Chave-Admin")] string? chaveAdmin,
        CancellationToken cancellationToken)
        => AdminAsync(chaveAdmin, () => _service.UpsertCategoriaAsync(pedido, cancellationToken));

    [HttpPost("produtos")]
    [EndpointSummary("Grava ou atualiza um produto da loja.")]
    public Task<IActionResult> Produto(
        ProdutoCatalogo pedido,
        [FromHeader(Name = "X-Chave-Admin")] string? chaveAdmin,
        CancellationToken cancellationToken)
        => AdminAsync(chaveAdmin, () => _service.UpsertProdutoAsync(pedido, cancellationToken));

    [HttpPost("formas-pagamento")]
    [EndpointSummary("Grava ou atualiza uma forma de pagamento da loja.")]
    public Task<IActionResult> Forma(
        FormaCatalogo pedido,
        [FromHeader(Name = "X-Chave-Admin")] string? chaveAdmin,
        CancellationToken cancellationToken)
        => AdminAsync(chaveAdmin, () => _service.UpsertFormaAsync(pedido, cancellationToken));

    [Authorize]
    [HttpPost("semeadura")]
    [EndpointSummary("Sobe o cadastro local uma vez, se a loja ainda não tiver cadastro central.")]
    public async Task<IActionResult> Semear(SemeaduraCatalogo pedido, CancellationToken cancellationToken)
    {
        try
        {
            await _service.SemearAsync(LojaDoToken(), pedido, cancellationToken);
            return Ok();
        }
        catch (Exception ex) when (ex is RecusaApiException or DomainException)
        {
            var recusa = ex as RecusaApiException;
            return StatusCode(recusa?.StatusCode ?? StatusCodes.Status400BadRequest, new { mensagem = ex.Message });
        }
    }

    [Authorize]
    [HttpGet]
    [EndpointSummary("Devolve o cadastro da loja alterado desde o instante informado.")]
    public async Task<IActionResult> Obter([FromQuery] DateTime? desde, CancellationToken cancellationToken)
    {
        var catalogo = await _service.ObterAsync(LojaDoToken(), desde, cancellationToken);
        return Ok(catalogo);
    }

    [Authorize]
    [HttpPost("precos")]
    [EndpointSummary("Grava só o preço de venda quando ele é mais recente que o da central.")]
    public async Task<IActionResult> Preco(PrecoCatalogo pedido, CancellationToken cancellationToken)
    {
        try
        {
            var preco = await _service.AplicarPrecoAsync(LojaDoToken(), pedido, cancellationToken);
            return Ok(preco);
        }
        catch (Exception ex) when (ex is RecusaApiException or DomainException)
        {
            var recusa = ex as RecusaApiException;
            return StatusCode(recusa?.StatusCode ?? StatusCodes.Status400BadRequest, new { mensagem = ex.Message });
        }
    }

    private async Task<IActionResult> AdminAsync<T>(string? chaveAdmin, Func<Task<T>> acao)
    {
        var esperada = _configuration["Sync:ChaveAdmin"] ?? string.Empty;
        if (string.IsNullOrEmpty(chaveAdmin) || !SegredoHasher.Iguais(chaveAdmin, esperada))
            return Unauthorized();

        try
        {
            return Ok(await acao());
        }
        catch (Exception ex) when (ex is RecusaApiException or DomainException)
        {
            var recusa = ex as RecusaApiException;
            return StatusCode(recusa?.StatusCode ?? StatusCodes.Status400BadRequest, new { mensagem = ex.Message });
        }
    }

    private Guid LojaDoToken() => Guid.Parse(User.FindFirst("store_id")!.Value);
}
