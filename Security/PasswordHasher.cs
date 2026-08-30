using System.Security.Cryptography;

namespace handytool_api.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 with a per-account random salt.
///
/// The salt is per account, not per application: a single shared salt lets one rainbow table cover
/// every user at once. Both halves are stored base64 so nothing depends on a text encoding round-trip.
/// </summary>
public static class PasswordHasher
{
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;

    /// <summary>OWASP's floor for PBKDF2-HMAC-SHA256 at the time of writing.</summary>
    private const int Iterations = 210_000;

    public static (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        return (Convert.ToBase64String(Derive(password, salt)), Convert.ToBase64String(salt));
    }

    /// <summary>
    /// Fixed-time comparison, so the time taken to reject a password says nothing about how much of
    /// the hash matched.
    /// </summary>
    public static bool Verify(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
        {
            return false;
        }

        byte[] expected;
        byte[] saltBytes;

        try
        {
            expected = Convert.FromBase64String(hash);
            saltBytes = Convert.FromBase64String(salt);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Derive(password, saltBytes), expected);
    }

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
}
