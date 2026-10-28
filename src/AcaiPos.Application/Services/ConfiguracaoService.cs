using AcaiPos.Application.Abstractions;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Services;

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

        var config = _db.Configuracoes.FirstOrDefault(item => item.DeletedAt == null);
        if (config is null)
        {
            config = Configuracao.Criar();
            _db.Add(config);
        }

        config.DefinirModoInterface(modo);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
