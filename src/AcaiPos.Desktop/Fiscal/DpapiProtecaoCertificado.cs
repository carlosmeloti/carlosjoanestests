using System.Security.Cryptography;
using System.Text;
using AcaiPos.Application.Abstractions;

namespace AcaiPos.Desktop.Fiscal;

public sealed class DpapiProtecaoCertificado : IProtecaoCertificado
{
    public string Proteger(string senha)
    {
        var protegida = ProtectedData.Protect(Encoding.UTF8.GetBytes(senha), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protegida);
    }

    public string Revelar(string senhaProtegida)
    {
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(senhaProtegida), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
