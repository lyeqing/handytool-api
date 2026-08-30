namespace handytool_api.Configuration;

/// <summary>
/// One rate-limit policy's numbers. <see cref="SegmentsPerWindow"/> above 1 makes it a sliding
/// window, which stops the burst-at-the-boundary that a fixed window allows: a caller cannot spend a
/// full window's allowance at 11:59:59 and another at 12:00:00.
/// </summary>
public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    /// <summary>1 for a fixed window; higher divides the window into that many sliding segments.</summary>
    public int SegmentsPerWindow { get; set; } = 1;

    /// <summary>
    /// Left at zero deliberately. Queueing a rejected request holds a connection open, which is the
    /// resource an abusive caller is trying to exhaust - failing fast with 429 is the point.
    /// </summary>
    public int QueueLimit { get; set; }
}

/// <summary>
/// Bound from the "RateLimiting" section of appsettings.json.
///
/// One limit for everything would be wrong in both directions: it would throttle the 30-second
/// analytics heartbeat while leaving login wide open. Each category gets numbers that suit what it
/// actually does.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Escape hatch for local debugging. Never turn this off in production.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Ordinary reads and writes. Partitioned by user id when signed in, by IP otherwise.</summary>
    public RateLimitPolicyOptions General { get; set; } = new()
    {
        PermitLimit = 200,
        WindowSeconds = 60,
        SegmentsPerWindow = 6
    };

    /// <summary>
    /// Page views and heartbeats. A visible tab beats twice a minute, so this has to tolerate a
    /// person with a dozen tabs open without ever tripping.
    /// </summary>
    public RateLimitPolicyOptions Analytics { get; set; } = new()
    {
        PermitLimit = 120,
        WindowSeconds = 60,
        SegmentsPerWindow = 6
    };

    /// <summary>Unbounded scans and anything else that costs real database work.</summary>
    public RateLimitPolicyOptions Expensive { get; set; } = new()
    {
        PermitLimit = 20,
        WindowSeconds = 60
    };

    /// <summary>
    /// Sign-in and registration. Always partitioned by IP - the caller is by definition not yet
    /// authenticated, so there is no user id to key on.
    /// </summary>
    public RateLimitPolicyOptions Login { get; set; } = new()
    {
        PermitLimit = 10,
        WindowSeconds = 60
    };

    /// <summary>
    /// Reserved. No password-reset endpoint exists yet; the policy is defined so that whoever adds
    /// one cannot forget to protect it, and so the thresholds are agreed in advance.
    /// </summary>
    public RateLimitPolicyOptions PasswordReset { get; set; } = new()
    {
        PermitLimit = 5,
        WindowSeconds = 900
    };
}
