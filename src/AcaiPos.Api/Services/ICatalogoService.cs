using AcaiPos.Api.Dtos;

namespace AcaiPos.Api.Services;

public interface ICatalogoService
{
    Task<CategoriaCatalogo> UpsertCategoriaAsync(CategoriaCatalogo pedido, CancellationToken cancellationToken);
    Task<ProdutoCatalogo> UpsertProdutoAsync(ProdutoCatalogo pedido, CancellationToken cancellationToken);
    Task<FormaCatalogo> UpsertFormaAsync(FormaCatalogo pedido, CancellationToken cancellationToken);
    Task SemearAsync(Guid lojaId, SemeaduraCatalogo pedido, CancellationToken cancellationToken);
    Task<CatalogoRemoto> ObterAsync(Guid lojaId, DateTime? desde, CancellationToken cancellationToken);
    Task<PrecoCatalogo> AplicarPrecoAsync(Guid lojaId, PrecoCatalogo pedido, CancellationToken cancellationToken);
}
