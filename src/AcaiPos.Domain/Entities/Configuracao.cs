using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class Configuracao : AuditableEntity
{
    public ModoInterface ModoInterface { get; private set; } = ModoInterface.Touchscreen;

    public bool ExibirBlocosProdutos { get; private set; } = true;

    public bool ExibirCategorias { get; private set; } = true;

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

    public void DefinirExibicaoVenda(bool blocosProdutos, bool categorias)
    {
        ExibirBlocosProdutos = blocosProdutos;
        ExibirCategorias = categorias;
        MarkUpdated();
    }
}
