using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class Configuracao : AuditableEntity
{
    public ModoInterface ModoInterface { get; private set; } = ModoInterface.Touchscreen;

    private Configuracao()
    {
    }

    public static Configuracao Criar() => new();

    public void DefinirModoInterface(ModoInterface modo)
    {
        if (!Enum.IsDefined(modo))
            throw new DomainException("Modo de interface inválido.");

        ModoInterface = modo;
        MarkUpdated();
    }
}
