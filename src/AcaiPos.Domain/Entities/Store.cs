using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class Store : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Store()
    {
    }

    public static Store Create(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Código da loja é obrigatório.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome da loja é obrigatório.");

        return new Store
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            IsActive = true
        };
    }
}
