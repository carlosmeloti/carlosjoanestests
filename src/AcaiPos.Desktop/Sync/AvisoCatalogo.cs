using AcaiPos.Application.Sync;

namespace AcaiPos.Desktop.Sync;

public sealed class AvisoCatalogo : IAvisoCatalogo
{
    private int _geracao;

    public int Geracao => Volatile.Read(ref _geracao);

    public event Action? Aplicado;

    public void CatalogoAplicado()
    {
        Interlocked.Increment(ref _geracao);
        Aplicado?.Invoke();
    }
}
