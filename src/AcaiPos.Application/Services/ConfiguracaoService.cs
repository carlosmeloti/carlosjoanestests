using AcaiPos.Application.Abstractions;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Services;

public sealed record ExibicaoVenda(bool BlocosProdutos, bool Categorias);

public sealed class ConfiguracaoService
{
    private readonly IPosDbContext _db;

    public ConfiguracaoService(IPosDbContext db)
    {
        _db = db;
    }

    public Task<ModoInterface> ObterModoInterfaceAsync()
    {
        var config = _db.Configuracoes.FirstOrDefault(item => item.DeletedAt == null);
        return Task.FromResult(config?.ModoInterface ?? ModoInterface.Touchscreen);
    }

    public async Task DefinirModoInterfaceAsync(ModoInterface modo, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(modo))
            throw new DomainException("Modo de interface inválido.");

        var config = ObterOuCriar();
        config.DefinirModoInterface(modo);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<ExibicaoVenda> ObterExibicaoVendaAsync()
    {
        var config = _db.Configuracoes.FirstOrDefault(item => item.DeletedAt == null);
        return Task.FromResult(new ExibicaoVenda(
            config?.ExibirBlocosProdutos ?? true,
            config?.ExibirCategorias ?? true));
    }

    public async Task DefinirExibicaoVendaAsync(
        bool blocosProdutos,
        bool categorias,
        CancellationToken cancellationToken = default)
    {
        var config = ObterOuCriar();
        config.DefinirExibicaoVenda(blocosProdutos, categorias);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private Configuracao ObterOuCriar()
    {
        var config = _db.Configuracoes.FirstOrDefault(item => item.DeletedAt == null);
        if (config is not null)
            return config;

        config = Configuracao.Criar();
        _db.Add(config);
        return config;
    }
}
