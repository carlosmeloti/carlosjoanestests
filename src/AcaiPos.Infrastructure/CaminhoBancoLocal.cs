namespace AcaiPos.Infrastructure;

public sealed class CaminhoBancoLocal
{
    public CaminhoBancoLocal(string caminho) => Caminho = caminho;

    public string Caminho { get; }
}
