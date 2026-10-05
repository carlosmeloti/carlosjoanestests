using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Domain.Entities;

public class User : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string PinHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    private User()
    {
    }

    public static User Create(string name, string username, string pinHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Login é obrigatório.");
        if (string.IsNullOrWhiteSpace(pinHash))
            throw new DomainException("PIN é obrigatório.");

        return new User
        {
            Name = name.Trim(),
            Username = username.Trim().ToLowerInvariant(),
            PinHash = pinHash,
            Role = role,
            IsActive = true
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkUpdated();
    }

    public void UpdatePin(string pinHash)
    {
        if (string.IsNullOrWhiteSpace(pinHash))
            throw new DomainException("PIN é obrigatório.");

        PinHash = pinHash;
        MarkUpdated();
    }

    public void Update(string name, UserRole role, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do usuário é obrigatório.");

        Name = name.Trim();
        Role = role;
        IsActive = isActive;
        MarkUpdated();
    }

    public bool CanCancelSales => Role is UserRole.Manager or UserRole.Administrator;
    public bool CanApplyDiscount => Role is UserRole.Manager or UserRole.Administrator;
    public bool CanManageProducts => Role is UserRole.Manager or UserRole.Administrator;
    public bool CanManageUsers => Role is UserRole.Administrator;
    public bool CanOpenCashRegister => IsActive;
}
