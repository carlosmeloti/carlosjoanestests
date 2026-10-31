using AcaiPos.Domain.Entities;

namespace AcaiPos.Api.Repositories;

public interface ITerminalRepository
{
    Task<Terminal?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Terminal?> ObterPorLojaECodigoAsync(Guid lojaId, string codigo, CancellationToken cancellationToken);

    void Adicionar(Terminal terminal);

    string? LerSegredoHash(Terminal terminal);

    void GravarSegredo(Terminal terminal, string hash, DateTime emitidoEm);
}
