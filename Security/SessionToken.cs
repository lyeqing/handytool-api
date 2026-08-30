using System.Security.Cryptography;
using System.Text;

namespace handytool_api.Security;

/// <summary>
/// Mints and hashes the opaque bearer tokens that stand in for a signed-in device.
///
/// The token is a credential, never an identity: it says nothing about which account it belongs to,
/// it is meaningless to the client, and it is deliberately not used as a VisitorId, SessionId or
/// UserId anywhere. Only <see cref="Hash"/> of it reaches the database, so the raw value exists in
/// exactly two places - the response to a successful login, and the client that stored it.
/// </summary>
public static class SessionToken
{
    /// <summary>256 bits of CSPRNG output. Far past guessing range, and short enough for a header.</summary>
    private const int TokenBytes = 32;

    public static string Create() => Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>
    /// SHA-256, unsalted and deliberately so. A salted, slow KDF would be right for passwords - they
    /// are low entropy and guessable. This token is 256 random bits, so there is nothing to guess,
    /// and the hash has to be recomputable in one indexed lookup on every authenticated request.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
