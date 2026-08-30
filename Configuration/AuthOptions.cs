namespace handytool_api.Configuration;

/// <summary>Bound from the "Auth" section of appsettings.json.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Absolute lifetime of a signed-in device. Long by design - revocation, not expiry, is the kill switch.</summary>
    public int SessionDays { get; set; } = 90;

    /// <summary>
    /// How stale <see cref="Models.UserSession.LastUsedDate"/> is allowed to get before a request
    /// bothers to write it. Without this, every authenticated request would be a database write.
    /// </summary>
    public int SessionTouchMinutes { get; set; } = 15;

    public int MinimumPasswordLength { get; set; } = 8;

    /// <summary>Cap on concurrent devices per account. Oldest active session is revoked past this.</summary>
    public int MaximumSessionsPerUser { get; set; } = 20;
}
