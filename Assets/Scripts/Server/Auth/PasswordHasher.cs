using System;
using System.Security.Cryptography;

/// <summary>
/// Dependency-free password hashing (1.4 / decision O2). PBKDF2 over the built-in
/// <see cref="Rfc2898DeriveBytes"/> (.NET Standard 2.1 — no NuGet). The stored value is a
/// self-describing PHC-style string "pbkdf2$&lt;iterations&gt;$&lt;salt_b64&gt;$&lt;subkey_b64&gt;",
/// so the work factor can rise later (or the algorithm swap to Argon2/BCrypt in 5.3) without a
/// schema change. Hashing/verification is CPU work — always run it off the main thread (it already
/// runs inside the authenticator's async DB lookup).
/// </summary>
public static class PasswordHasher
{
    const int SaltSize   = 16;       // 128-bit salt
    const int KeySize    = 32;       // 256-bit derived subkey
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
            salt     = Convert.FromBase64String(parts[2]);
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
