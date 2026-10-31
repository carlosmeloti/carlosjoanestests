using AcaiPos.Api.Dtos;
using AcaiPos.Api.Persistence;
using AcaiPos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Api.Services;

public sealed class CatalogoService : ICatalogoService
{
    private readonly CentralDbContext _db;

    public CatalogoService(CentralDbContext db) => _db = db;

    public async Task<CategoriaCatalogo> UpsertCategoriaAsync(CategoriaCatalogo pedido, CancellationToken cancellationToken)
    {
        ExigirLoja(pedido.LojaId);
        ExigirId(pedido.Id);
        var atual = await CategoriaAsync(pedido.LojaId, pedido.Id, cancellationToken);
        var quando = pedido.UpdatedAt ?? DateTime.UtcNow;
        if (atual is null)
        {
            atual = Category.Create(pedido.Name, pedido.Description, pedido.SortOrder);
            atual.AdotarId(pedido.Id);
            _db.Add(atual);
        }

        if (!pedido.IsActive || pedido.DeletedAt is not null)
            atual.Update(pedido.Name, pedido.Description, pedido.SortOrder, false);
        else
            atual.Update(pedido.Name, pedido.Description, pedido.SortOrder, true);
        atual.DefinirPadrao(pedido.IsDefault);
        atual.AplicarTrilhaCentral(quando, pedido.DeletedAt);
        MarcarLoja(atual, pedido.LojaId);
        await _db.SaveChangesAsync(cancellationToken);
        return Mapear(atual, pedido.LojaId);
    }

    public async Task<ProdutoCatalogo> UpsertProdutoAsync(ProdutoCatalogo pedido, CancellationToken cancellationToken)
    {
        ExigirLoja(pedido.LojaId);
        ExigirId(pedido.Id);
        await ExigirCategoriasAsync(pedido.LojaId, pedido.CategoryIds, cancellationToken);
        var quando = pedido.UpdatedAt ?? DateTime.UtcNow;
        var atual = await ProdutoAsync(pedido.LojaId, pedido.Id, cancellationToken);
        if (atual is null)
        {
            atual = Product.Create(
                pedido.Name,
                pedido.Sku,
                pedido.CategoryIds,
                pedido.SalePrice,
                pedido.Barcode,
                pedido.CostPrice,
                pedido.StockQuantity,
                pedido.UnitOfMeasure,
                pedido.Ncm,
                pedido.Cfop,
                pedido.Origem,
                pedido.Csosn,
                pedido.Cest,
                pedido.PisCst,
                pedido.CofinsCst,
                pedido.IbsCbsCst,
                pedido.CClassTrib);
            atual.AdotarId(pedido.Id);
            atual.RedefinirCategorias(pedido.CategoryIds);
            if (!pedido.IsActive)
                atual.Update(atual.Name, atual.Sku, pedido.CategoryIds, atual.SalePrice, atual.Barcode, atual.CostPrice, atual.UnitOfMeasure, false);
            _db.Add(atual);
        }
        else
        {
            var delta = pedido.StockQuantity - atual.StockQuantity;
            if (delta != 0)
                atual.AdjustStock(delta);
            atual.Update(
                pedido.Name,
                pedido.Sku,
                pedido.CategoryIds,
                pedido.SalePrice,
                pedido.Barcode,
                pedido.CostPrice,
                pedido.UnitOfMeasure,
                pedido.IsActive && pedido.DeletedAt is null,
                pedido.Ncm,
                pedido.Cfop,
                pedido.Origem,
                pedido.Csosn,
                pedido.Cest,
                pedido.PisCst,
                pedido.CofinsCst,
                pedido.IbsCbsCst,
                pedido.CClassTrib,
                atualizarDadosFiscais: true);
        }

        atual.AplicarTrilhaCentral(quando, pedido.DeletedAt);
        MarcarLoja(atual, pedido.LojaId);
        await _db.SaveChangesAsync(cancellationToken);
        return Mapear(atual, pedido.LojaId);
    }

    public async Task<FormaCatalogo> UpsertFormaAsync(FormaCatalogo pedido, CancellationToken cancellationToken)
    {
        ExigirLoja(pedido.LojaId);
        ExigirId(pedido.Id);
        var quando = pedido.UpdatedAt ?? DateTime.UtcNow;
        var atual = await FormaAsync(pedido.LojaId, pedido.Id, cancellationToken);
        if (atual is null)
        {
            atual = FormaPagamento.Criar(
                pedido.Id,
                pedido.Descricao,
                pedido.CalculaTroco,
                pedido.IsParcela,
                pedido.CorRgbBotao,
                pedido.Ordem,
                pedido.IsEnable,
                pedido.CodigoTpag);
            _db.Add(atual);
        }
        else
        {
            atual.Atualizar(
                pedido.Descricao,
                pedido.CalculaTroco,
                pedido.IsParcela,
                pedido.CorRgbBotao,
                pedido.Ordem,
                pedido.IsEnable,
                pedido.CodigoTpag);
        }

        atual.AplicarCentral(
            atual.Descricao,
            atual.CalculaTroco,
            atual.IsParcela,
            atual.CorRgbBotao,
            atual.Ordem,
            atual.IsEnable,
            atual.CodigoTpag,
            quando,
            pedido.DeletedAt);
        MarcarLoja(atual, pedido.LojaId);
        await _db.SaveChangesAsync(cancellationToken);
        return Mapear(atual, pedido.LojaId);
    }

    public async Task SemearAsync(Guid lojaId, SemeaduraCatalogo pedido, CancellationToken cancellationToken)
    {
        if (await TemCadastroAsync(lojaId, cancellationToken))
            throw new RecusaApiException("Loja já possui cadastro.", StatusCodes.Status409Conflict);

        await using var transacao = await _db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var categoria in pedido.Categorias)
        {
            categoria.LojaId = lojaId;
            await UpsertCategoriaAsync(categoria, cancellationToken);
        }

        foreach (var forma in pedido.FormasPagamento)
        {
            forma.LojaId = lojaId;
            await UpsertFormaAsync(forma, cancellationToken);
        }

        foreach (var produto in pedido.Produtos)
        {
            produto.LojaId = lojaId;
            await UpsertProdutoAsync(produto, cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);
    }

    public async Task<CatalogoRemoto> ObterAsync(Guid lojaId, DateTime? desde, CancellationToken cancellationToken)
    {
        var categorias = await _db.Categories
            .Where(item => EF.Property<Guid>(item, "LojaId") == lojaId)
            .Where(item => desde == null || item.UpdatedAt > desde || item.DeletedAt > desde)
            .ToListAsync(cancellationToken);
        var produtos = await _db.Products
            .Include(item => item.Categories)
            .Where(item => EF.Property<Guid>(item, "LojaId") == lojaId)
            .Where(item => desde == null || item.UpdatedAt > desde || item.DeletedAt > desde)
            .ToListAsync(cancellationToken);
        var formas = await _db.FormasPagamento
            .Where(item => EF.Property<Guid>(item, "LojaId") == lojaId)
            .Where(item => desde == null || item.UpdatedAt > desde || item.DeletedAt > desde)
            .ToListAsync(cancellationToken);

        return new CatalogoRemoto
        {
            Categorias = categorias.Select(item => Mapear(item, lojaId)).ToList(),
            Produtos = produtos.Select(item => Mapear(item, lojaId)).ToList(),
            FormasPagamento = formas.Select(item => Mapear(item, lojaId)).ToList()
        };
    }

    public async Task<PrecoCatalogo> AplicarPrecoAsync(Guid lojaId, PrecoCatalogo pedido, CancellationToken cancellationToken)
    {
        ExigirId(pedido.Id);
        var produto = await ProdutoAsync(lojaId, pedido.Id, cancellationToken)
            ?? throw new RecusaApiException("Produto não encontrado.");
        if (produto.DeletedAt is not null)
            throw new RecusaApiException("Produto removido na central.");
        if (pedido.UpdatedAt <= produto.UpdatedAt)
            return new PrecoCatalogo { Id = produto.Id, SalePrice = produto.SalePrice, UpdatedAt = produto.UpdatedAt };

        produto.DefinirPrecoVenda(pedido.SalePrice);
        produto.AplicarTrilhaCentral(pedido.UpdatedAt, null);
        await _db.SaveChangesAsync(cancellationToken);
        return new PrecoCatalogo { Id = produto.Id, SalePrice = produto.SalePrice, UpdatedAt = produto.UpdatedAt };
    }

    private async Task<bool> TemCadastroAsync(Guid lojaId, CancellationToken cancellationToken)
        => await _db.Categories.AnyAsync(item => EF.Property<Guid>(item, "LojaId") == lojaId, cancellationToken)
           || await _db.Products.AnyAsync(item => EF.Property<Guid>(item, "LojaId") == lojaId, cancellationToken)
           || await _db.FormasPagamento.AnyAsync(item => EF.Property<Guid>(item, "LojaId") == lojaId, cancellationToken);

    private Task<Category?> CategoriaAsync(Guid lojaId, Guid id, CancellationToken cancellationToken)
        => _db.Categories.FirstOrDefaultAsync(
            item => item.Id == id && EF.Property<Guid>(item, "LojaId") == lojaId,
            cancellationToken);

    private Task<Product?> ProdutoAsync(Guid lojaId, Guid id, CancellationToken cancellationToken)
        => _db.Products.Include(item => item.Categories).FirstOrDefaultAsync(
            item => item.Id == id && EF.Property<Guid>(item, "LojaId") == lojaId,
            cancellationToken);

    private Task<FormaPagamento?> FormaAsync(Guid lojaId, Guid id, CancellationToken cancellationToken)
        => _db.FormasPagamento.FirstOrDefaultAsync(
            item => item.Id == id && EF.Property<Guid>(item, "LojaId") == lojaId,
            cancellationToken);

    private async Task ExigirCategoriasAsync(Guid lojaId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || ids.Any(id => id == Guid.Empty))
            throw new RecusaApiException("Selecione ao menos uma categoria.");

        var existentes = await _db.Categories
            .Where(item => EF.Property<Guid>(item, "LojaId") == lojaId && ids.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (existentes.Count != ids.Distinct().Count())
            throw new RecusaApiException("Categoria não encontrada nesta loja.");
    }

    private void MarcarLoja(object entidade, Guid lojaId)
        => _db.Entry(entidade).Property("LojaId").CurrentValue = lojaId;

    private static void ExigirLoja(Guid lojaId)
    {
        if (lojaId == Guid.Empty)
            throw new RecusaApiException("Loja do cadastro é obrigatória.");
    }

    private static void ExigirId(Guid id)
    {
        if (id == Guid.Empty)
            throw new RecusaApiException("Identificador é obrigatório.");
    }

    private static CategoriaCatalogo Mapear(Category categoria, Guid lojaId) => new()
    {
        Id = categoria.Id,
        LojaId = lojaId,
        Name = categoria.Name,
        Description = categoria.Description,
        SortOrder = categoria.SortOrder,
        IsActive = categoria.IsActive,
        IsDefault = categoria.IsDefault,
        UpdatedAt = categoria.UpdatedAt,
        DeletedAt = categoria.DeletedAt
    };

    private static ProdutoCatalogo Mapear(Product produto, Guid lojaId) => new()
    {
        Id = produto.Id,
        LojaId = lojaId,
        Name = produto.Name,
        Sku = produto.Sku,
        Barcode = produto.Barcode,
        CategoryIds = produto.CategoryIds.ToList(),
        SalePrice = produto.SalePrice,
        CostPrice = produto.CostPrice,
        StockQuantity = produto.StockQuantity,
        UnitOfMeasure = produto.UnitOfMeasure,
        IsActive = produto.IsActive,
        Ncm = produto.Ncm,
        Cfop = produto.Cfop,
        Origem = produto.Origem,
        Csosn = produto.Csosn,
        Cest = produto.Cest,
        PisCst = produto.PisCst,
        CofinsCst = produto.CofinsCst,
        IbsCbsCst = produto.IbsCbsCst,
        CClassTrib = produto.CClassTrib,
        UpdatedAt = produto.UpdatedAt,
        DeletedAt = produto.DeletedAt
    };

    private static FormaCatalogo Mapear(FormaPagamento forma, Guid lojaId) => new()
    {
        Id = forma.Id,
        LojaId = lojaId,
        Descricao = forma.Descricao,
        CalculaTroco = forma.CalculaTroco,
        IsParcela = forma.IsParcela,
        CorRgbBotao = forma.CorRgbBotao,
        Ordem = forma.Ordem,
        IsEnable = forma.IsEnable,
        CodigoTpag = forma.CodigoTpag,
        UpdatedAt = forma.UpdatedAt,
        DeletedAt = forma.DeletedAt
    };
}
