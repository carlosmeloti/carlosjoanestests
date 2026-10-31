namespace AcaiPos.Application.Sync;

public interface IAvisoCatalogo
{
    int Geracao { get; }

    event Action? Aplicado;

    void CatalogoAplicado();
}
