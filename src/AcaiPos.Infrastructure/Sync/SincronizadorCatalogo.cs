using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Sync;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;
using AcaiPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Infrastructure.Sync;

public sealed class SincronizadorCatalogo
{
    private readonly CaminhoBancoLocal _caminho;
    private readonly IProtecaoCertificado _protecao;
    private readonly ISyncTransporte _transporte;
    private readonly IAvisoCatalogo _aviso;

    public SincronizadorCatalogo(
        CaminhoBancoLocal caminho,
        IProtecaoCertificado protecao,
        ISyncTransporte transporte,
        IAvisoCatalogo aviso)
    {
        _caminho = caminho;
        _protecao = protecao;
        _transporte = transporte;
        _aviso = aviso;
    }

    public async Task SincronizarAsync(CancellationToken cancellationToken)
    {
        await using var db = Criar();
        var inscricao = await db.InscricoesTerminais.FirstOrDefaultAsync(cancellationToken);
        if (inscricao is null || !inscricao.Completa)
            return;

        var segredo = _protecao.Revelar(inscricao.SegredoProtegido);
        if (inscricao.CatalogoLidoEm is null)
        {
            var semeadura = await _transporte.SemearCatalogoAsync(
                inscricao.EnderecoApi,
                inscricao.TerminalId,
                segredo,
                await MontarSemeaduraAsync(db, cancellationToken),
                cancellationToken);

            if (semeadura.Tipo == TipoSemeadura.Aceita)
            {
                inscricao.MarcarCatalogoLido(DateTime.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (semeadura.Tipo == TipoSemeadura.JaExiste)
            {
                if (await VendaAbertaAsync(db, cancellationToken))
                    return;

                var catalogo = await _transporte.ObterCatalogoAsync(
                    inscricao.EnderecoApi,
                    inscricao.TerminalId,
                    segredo,
                    null,
                    cancellationToken);
                if (catalogo is null)
                    return;

                await AplicarAsync(db, catalogo, cancellationToken);
                inscricao.MarcarCatalogoLido(DateTime.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                AvisarSeHouveLinha(catalogo);
            }
            else
            {
                return;
            }
        }
        else if (!await VendaAbertaAsync(db, cancellationToken))
        {
            var catalogo = await _transporte.ObterCatalogoAsync(
                inscricao.EnderecoApi,
                inscricao.TerminalId,
                segredo,
                inscricao.CatalogoLidoEm,
                cancellationToken);
            if (catalogo is not null)
            {
                await AplicarAsync(db, catalogo, cancellationToken);
                inscricao.MarcarCatalogoLido(DateTime.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                AvisarSeHouveLinha(catalogo);
            }
        }

        await EnviarPrecosAsync(db, inscricao, segredo, cancellationToken);
    }

    private void AvisarSeHouveLinha(CatalogoRecebido catalogo)
    {
        if (catalogo.Categorias.Count > 0 || catalogo.Produtos.Count > 0 || catalogo.FormasPagamento.Count > 0)
            _aviso.CatalogoAplicado();
    }

    private async Task EnviarPrecosAsync(PosDbContext db, InscricaoTerminal inscricao, string segredo, CancellationToken cancellationToken)
    {
        if (inscricao.CatalogoLidoEm is null)
            return;

        var pendentes = await db.SyncOperations
            .Where(item => item.Status == SyncStatus.Pending && item.AggregateType == SyncAggregateType.ProductPrice)
            .ToListAsync(cancellationToken);

        foreach (var operacao in pendentes)
        {
            var produto = await db.Products.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken);
            if (produto is null)
            {
                operacao.RegistrarRecusa("Produto não encontrado.");
                db.SyncErrors.Add(SyncError.Create(operacao.Id, operacao.LastError!));
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            var resultado = await _transporte.EnviarPrecoAsync(
                inscricao.EnderecoApi,
                inscricao.TerminalId,
                segredo,
                new PrecoRemoto { Id = produto.Id, SalePrice = produto.SalePrice, UpdatedAt = produto.UpdatedAt },
                cancellationToken);

            if (resultado.Aceito && resultado.Preco is not null)
            {
                if (resultado.Preco.SalePrice != produto.SalePrice || resultado.Preco.UpdatedAt != produto.UpdatedAt)
                {
                    produto.DefinirPrecoVenda(resultado.Preco.SalePrice);
                    produto.AplicarTrilhaCentral(resultado.Preco.UpdatedAt, produto.DeletedAt);
                }

                operacao.MarcarSincronizada();
            }
            else if (resultado.HttpStatus == 400)
            {
                operacao.RegistrarRecusa(resultado.Mensagem ?? "Preço recusado.");
                db.SyncErrors.Add(SyncError.Create(operacao.Id, operacao.LastError!, resultado.HttpStatus));
                produto.MarcarFalhaSincronizacao();
            }
            else
            {
                operacao.RegistrarFalha(resultado.Mensagem ?? "Falha de rede.");
                db.SyncErrors.Add(SyncError.Create(operacao.Id, operacao.LastError!, resultado.HttpStatus));
                if (operacao.Status == SyncStatus.Error)
                    produto.MarcarFalhaSincronizacao();
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task AplicarAsync(PosDbContext db, CatalogoRecebido catalogo, CancellationToken cancellationToken)
    {
        foreach (var recebida in catalogo.Categorias)
        {
            var atual = await db.Categories.FirstOrDefaultAsync(item => item.Id == recebida.Id, cancellationToken);
            if (atual is null)
            {
                atual = Category.Create(recebida.Name, recebida.Description, recebida.SortOrder);
                atual.AdotarId(recebida.Id);
                db.Add(atual);
            }

            atual.Update(recebida.Name, recebida.Description, recebida.SortOrder, recebida.IsActive && recebida.DeletedAt is null);
            atual.DefinirPadrao(recebida.IsDefault);
            atual.AplicarTrilhaCentral(recebida.UpdatedAt ?? DateTime.UtcNow, recebida.DeletedAt);
        }

        foreach (var recebida in catalogo.FormasPagamento)
        {
            var atual = await db.FormasPagamento.FirstOrDefaultAsync(item => item.Id == recebida.Id, cancellationToken);
            if (atual is null)
            {
                atual = FormaPagamento.Criar(
                    recebida.Id,
                    recebida.Descricao,
                    recebida.CalculaTroco,
                    recebida.IsParcela,
                    recebida.CorRgbBotao,
                    recebida.Ordem,
                    recebida.IsEnable,
                    recebida.CodigoTpag);
                db.Add(atual);
            }

            atual.AplicarCentral(
                recebida.Descricao,
                recebida.CalculaTroco,
                recebida.IsParcela,
                recebida.CorRgbBotao,
                recebida.Ordem,
                recebida.IsEnable,
                recebida.CodigoTpag,
                recebida.UpdatedAt ?? DateTime.UtcNow,
                recebida.DeletedAt);
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var recebido in catalogo.Produtos)
        {
            var atual = await db.Products.FirstOrDefaultAsync(item => item.Id == recebido.Id, cancellationToken);
            var quando = recebido.UpdatedAt ?? DateTime.UtcNow;
            if (atual is null)
            {
                atual = Product.Create(
                    recebido.Name,
                    recebido.Sku,
                    recebido.CategoryIds,
                    recebido.SalePrice,
                    recebido.Barcode,
                    recebido.CostPrice,
                    recebido.StockQuantity,
                    recebido.UnitOfMeasure,
                    recebido.Ncm,
                    recebido.Cfop,
                    recebido.Origem,
                    recebido.Csosn,
                    recebido.Cest,
                    recebido.PisCst,
                    recebido.CofinsCst,
                    recebido.IbsCbsCst,
                    recebido.CClassTrib);
                atual.AdotarId(recebido.Id);
                atual.RedefinirCategorias(recebido.CategoryIds);
                if (!recebido.IsActive)
                    atual.Update(atual.Name, atual.Sku, recebido.CategoryIds, atual.SalePrice, atual.Barcode, atual.CostPrice, atual.UnitOfMeasure, false);
                db.Add(atual);
                atual.AplicarTrilhaCentral(quando, recebido.DeletedAt);
                continue;
            }

            db.Entry(atual).Collection(item => item.Categories).Load();
            var precoLocalVence = await PrecoLocalMaisRecenteAsync(db, atual, quando, cancellationToken);
            var preco = precoLocalVence ? atual.SalePrice : recebido.SalePrice;
            var trilha = precoLocalVence ? atual.UpdatedAt : quando;
            atual.Update(
                recebido.Name,
                recebido.Sku,
                recebido.CategoryIds,
                preco,
                recebido.Barcode,
                recebido.CostPrice,
                recebido.UnitOfMeasure,
                recebido.IsActive && recebido.DeletedAt is null,
                recebido.Ncm,
                recebido.Cfop,
                recebido.Origem,
                recebido.Csosn,
                recebido.Cest,
                recebido.PisCst,
                recebido.CofinsCst,
                recebido.IbsCbsCst,
                recebido.CClassTrib,
                atualizarDadosFiscais: true);
            atual.AplicarTrilhaCentral(trilha, recebido.DeletedAt);
            if (!precoLocalVence)
                await EncerrarPrecoPendenteAsync(db, atual.Id, cancellationToken);
        }
    }

    private static async Task<bool> PrecoLocalMaisRecenteAsync(
        PosDbContext db,
        Product produto,
        DateTime central,
        CancellationToken cancellationToken)
    {
        var pendente = await db.SyncOperations.AnyAsync(
            item => item.AggregateType == SyncAggregateType.ProductPrice
                && item.AggregateId == produto.Id
                && item.Status == SyncStatus.Pending,
            cancellationToken);
        return pendente && produto.UpdatedAt.Ticks > central.Ticks;
    }

    private static async Task EncerrarPrecoPendenteAsync(PosDbContext db, Guid produtoId, CancellationToken cancellationToken)
    {
        var operacao = await db.SyncOperations.FirstOrDefaultAsync(
            item => item.AggregateType == SyncAggregateType.ProductPrice && item.AggregateId == produtoId && item.Status == SyncStatus.Pending,
            cancellationToken);
        operacao?.MarcarSincronizada();
    }

    private static async Task<SemeaduraRemota> MontarSemeaduraAsync(PosDbContext db, CancellationToken cancellationToken)
    {
        var categorias = await db.Categories.ToListAsync(cancellationToken);
        var produtos = await db.Products.Include(item => item.Categories).ToListAsync(cancellationToken);
        var formas = await db.FormasPagamento.ToListAsync(cancellationToken);
        return new SemeaduraRemota
        {
            Categorias = categorias.Select(item => new CategoriaRemota
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                SortOrder = item.SortOrder,
                IsActive = item.IsActive,
                IsDefault = item.IsDefault,
                UpdatedAt = item.UpdatedAt,
                DeletedAt = item.DeletedAt
            }).ToList(),
            Produtos = produtos.Select(item => new ProdutoRemoto
            {
                Id = item.Id,
                Name = item.Name,
                Sku = item.Sku,
                Barcode = item.Barcode,
                CategoryIds = item.CategoryIds.ToList(),
                SalePrice = item.SalePrice,
                CostPrice = item.CostPrice,
                StockQuantity = item.StockQuantity,
                UnitOfMeasure = item.UnitOfMeasure,
                IsActive = item.IsActive,
                Ncm = item.Ncm,
                Cfop = item.Cfop,
                Origem = item.Origem,
                Csosn = item.Csosn,
                Cest = item.Cest,
                PisCst = item.PisCst,
                CofinsCst = item.CofinsCst,
                IbsCbsCst = item.IbsCbsCst,
                CClassTrib = item.CClassTrib,
                UpdatedAt = item.UpdatedAt,
                DeletedAt = item.DeletedAt
            }).ToList(),
            FormasPagamento = formas.Select(item => new FormaRemota
            {
                Id = item.Id,
                Descricao = item.Descricao,
                CalculaTroco = item.CalculaTroco,
                IsParcela = item.IsParcela,
                CorRgbBotao = item.CorRgbBotao,
                Ordem = item.Ordem,
                IsEnable = item.IsEnable,
                CodigoTpag = item.CodigoTpag,
                UpdatedAt = item.UpdatedAt,
                DeletedAt = item.DeletedAt
            }).ToList()
        };
    }

    private static Task<bool> VendaAbertaAsync(PosDbContext db, CancellationToken cancellationToken)
        => db.Sales.AnyAsync(item => item.Status == SaleStatus.Open && item.DeletedAt == null, cancellationToken);

    private PosDbContext Criar()
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite($"Data Source={_caminho.Caminho}")
            .Options;
        return new PosDbContext(options);
    }
}
