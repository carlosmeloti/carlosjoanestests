using System.Security.Cryptography;

namespace AcaiPos.Api.Seguranca;

public static class SegredoHasher
{
    public static string Gerar()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string segredo)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(segredo, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Confere(string segredo, string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return false;

        var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations))
            return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(segredo, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool Iguais(string informado, string esperado)
    {
        var left = System.Text.Encoding.UTF8.GetBytes(informado);
        var right = System.Text.Encoding.UTF8.GetBytes(esperado);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
