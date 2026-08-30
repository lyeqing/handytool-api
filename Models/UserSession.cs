namespace handytool_api.Models;

/// <summary>
/// One signed-in device. This is the AUTHENTICATION session and has nothing to do with
/// <see cref="TrackingSession"/>, which is a period of browsing.
///
/// The token handed to the client is opaque random bytes and is never stored: only
/// <see cref="TokenHash"/> lands in the database, so a database leak does not yield usable
/// credentials. A user may hold many of these at once - Chrome, an iPhone and an Android phone are
/// three rows here and one <see cref="UserId"/>.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; }

    /// <summary>The stable account identity this credential resolves to.</summary>
    public long UserId { get; set; }

    /// <summary>SHA-256 of the raw token, base64. Uniquely indexed - this is the lookup key.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Which kind of client holds this session. Purely informational for the user.</summary>
    public ClientType ClientType { get; set; } = ClientType.Web;

    /// <summary>User-facing label shown in "your devices", for example "Chrome on Windows".</summary>
    public string? DeviceName { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedDate { get; set; }

    /// <summary>Touched on use, at most once per <c>Auth:SessionTouchMinutes</c>, to avoid a write per request.</summary>
    public DateTime LastUsedDate { get; set; }

    /// <summary>Absolute expiry. Past this the session is dead regardless of activity.</summary>
    public DateTime ExpiresDate { get; set; }

    /// <summary>Set by logout or by an administrator. A revoked session can never be revived.</summary>
    public DateTime? RevokedDate { get; set; }

    public UserAccount User { get; set; } = null!;

    /// <summary>A session is usable only while it is neither revoked nor past its absolute expiry.</summary>
    public bool IsActiveAt(DateTime utcNow) => RevokedDate is null && ExpiresDate > utcNow;
}
