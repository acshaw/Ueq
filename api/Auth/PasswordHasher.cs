using System.Security.Cryptography;

namespace Ueq.ContentApi.Auth;

/// <summary>
/// Mirrors the game server's <c>Assets/Scripts/Server/Auth/PasswordHasher.cs</c> exactly (5.11) —
/// same PHC-style "pbkdf2$&lt;iterations&gt;$&lt;salt_b64&gt;$&lt;subkey_b64&gt;" format, same work
/// factor. Not shared code (separate project, separate concern — web admin vs. game accounts) but
/// deliberately the identical scheme rather than inventing a second one.
/// </summary>
public static class PasswordHasher
{
    const int SaltSize = 16;
    const int KeySize = 32;
    const int Iterations = 100_000;

    public static string Hash(string password)
    {
        byte[] salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(salt);

        byte[] subkey = Derive(password, salt, Iterations);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}";
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
        if (!int.TryParse(parts[1], out int iterations) || iterations <= 0) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException) { return false; }

        byte[] actual = Derive(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    static byte[] Derive(string password, byte[] salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }
}
