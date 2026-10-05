using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class Terminal : AuditableEntity
{
    public Guid StoreId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public Store? Store { get; private set; }

    private Terminal()
    {
    }

    public static Terminal Create(Guid storeId, string code, string name)
    {
        if (storeId == Guid.Empty)
            throw new DomainException("Loja é obrigatória.");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Código do terminal é obrigatório.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do terminal é obrigatório.");

        return new Terminal
        {
            StoreId = storeId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            IsActive = true
        };
    }
}
