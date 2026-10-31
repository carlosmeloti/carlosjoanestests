using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Sync;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;
using AcaiPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcaiPos.Infrastructure.Sync;

public sealed class SincronizadorFila
{
    private readonly CaminhoBancoLocal _caminho;
    private readonly IProtecaoCertificado _protecao;
    private readonly ISyncTransporte _transporte;
    private readonly SincronizadorCatalogo _catalogo;

    public SincronizadorFila(
        CaminhoBancoLocal caminho,
        IProtecaoCertificado protecao,
        ISyncTransporte transporte,
        SincronizadorCatalogo catalogo)
    {
        _caminho = caminho;
        _protecao = protecao;
        _transporte = transporte;
        _catalogo = catalogo;
    }

    public async Task ExecutarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _catalogo.SincronizarAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
        }

        List<Envio> envios;
        await using (var db = Criar())
        {
            var inscricao = await db.InscricoesTerminais.FirstOrDefaultAsync(cancellationToken);
            if (inscricao is null || !inscricao.Completa)
                return;

            var lojaId = inscricao.LojaId;
            var terminalId = inscricao.TerminalId;
            var segredo = _protecao.Revelar(inscricao.SegredoProtegido);
            var endereco = inscricao.EnderecoApi;

            var pendentes = await db.SyncOperations
                .Where(operacao => operacao.Status == SyncStatus.Pending
                    && operacao.AggregateType != SyncAggregateType.ProductPrice)
                .OrderBy(operacao => operacao.UpdatedAt)
                .ToListAsync(cancellationToken);

            envios = [];
            foreach (var operacao in pendentes)
            {
                var corpo = await MontarAsync(db, operacao, lojaId, terminalId, cancellationToken);
                if (corpo is null)
                {
                    operacao.RegistrarRecusa("Agregado não encontrado.");
                    db.SyncErrors.Add(SyncError.Create(operacao.Id, operacao.LastError!));
                    await MarcarAgregadoAsync(db, operacao, sincronizado: false, cancellationToken);
                    continue;
                }

                envios.Add(new Envio(operacao.Id, operacao.UpdatedAt.Ticks, endereco, terminalId, segredo, corpo));
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var envio in envios)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResultadoEnvio resultado;
            try
            {
                resultado = await _transporte.EnviarAsync(
                    envio.EnderecoApi,
                    envio.TerminalId,
                    envio.Segredo,
                    envio.Corpo,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                resultado = ResultadoEnvio.Transitorio(null, "Falha de rede.");
            }

            await using var db = Criar();
            var operacao = await db.SyncOperations.FirstOrDefaultAsync(item => item.Id == envio.OperacaoId, cancellationToken);
            if (operacao is null || operacao.Status != SyncStatus.Pending || operacao.UpdatedAt.Ticks != envio.AtualizadoEmTicks)
                continue;

            switch (resultado.Tipo)
            {
                case TipoResultadoEnvio.Aceito:
                    operacao.MarcarSincronizada();
                    await MarcarAgregadoAsync(db, operacao, sincronizado: true, cancellationToken);
                    break;
                case TipoResultadoEnvio.Recusado:
                    operacao.RegistrarRecusa(resultado.Mensagem ?? "Lote recusado.");
                    db.SyncErrors.Add(SyncError.Create(operacao.Id, operacao.LastError!, resultado.HttpStatus));
                    await MarcarAgregadoAsync(db, operacao, sincronizado: false, cancellationToken);
                    break;
                default:
                    operacao.RegistrarFalha(resultado.Mensagem ?? "Falha de rede.");
                    db.SyncErrors.Add(SyncError.Create(operacao.Id, operacao.LastError!, resultado.HttpStatus));
                    if (operacao.Status == SyncStatus.Error)
                        await MarcarAgregadoAsync(db, operacao, sincronizado: false, cancellationToken);
                    break;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private PosDbContext Criar()
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite($"Data Source={_caminho.Caminho}")
            .Options;
        return new PosDbContext(options);
    }

    private static async Task<CorpoLote?> MontarAsync(
        PosDbContext db,
        SyncOperation operacao,
        Guid lojaId,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        switch (operacao.AggregateType)
        {
            case SyncAggregateType.Sale:
                var venda = await db.Sales.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken);
                if (venda is null)
                    return null;
                var itens = await db.SaleItems.Where(item => item.SaleId == venda.Id).ToListAsync(cancellationToken);
                var pagamentos = await db.Payments.Where(item => item.SaleId == venda.Id).ToListAsync(cancellationToken);
                return MontadorLote.DeVenda(venda, itens, pagamentos, lojaId, terminalId);
            case SyncAggregateType.CashRegister:
                var caixa = await db.CashRegisters.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken);
                return caixa is null ? null : MontadorLote.DeCaixa(caixa, lojaId, terminalId);
            case SyncAggregateType.CashMovement:
                var movimento = await db.CashMovements.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken);
                return movimento is null ? null : MontadorLote.DeMovimentoCaixa(movimento, terminalId);
            case SyncAggregateType.StockMovement:
                var estoque = await db.StockMovements.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken);
                return estoque is null ? null : MontadorLote.DeMovimentoEstoque(estoque);
            default:
                return null;
        }
    }

    private static async Task MarcarAgregadoAsync(
        PosDbContext db,
        SyncOperation operacao,
        bool sincronizado,
        CancellationToken cancellationToken)
    {
        AuditableEntity? agregado = operacao.AggregateType switch
        {
            SyncAggregateType.Sale => await db.Sales.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken),
            SyncAggregateType.CashRegister => await db.CashRegisters.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken),
            SyncAggregateType.CashMovement => await db.CashMovements.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken),
            SyncAggregateType.StockMovement => await db.StockMovements.FirstOrDefaultAsync(item => item.Id == operacao.AggregateId, cancellationToken),
            _ => null
        };

        if (agregado is null)
            return;

        if (sincronizado)
            agregado.MarkSynced();
        else
            agregado.MarcarFalhaSincronizacao();
    }

    private sealed record Envio(
        Guid OperacaoId,
        long AtualizadoEmTicks,
        string EnderecoApi,
        Guid TerminalId,
        string Segredo,
        CorpoLote Corpo);
}
