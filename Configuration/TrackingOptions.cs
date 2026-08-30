namespace handytool_api.Configuration;

/// <summary>Bound from the "Tracking" section of appsettings.json.</summary>
public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    /// <summary>How often a visible tab should beat. Served to the browser so it is changed in one place.</summary>
    public int HeartbeatSeconds { get; set; } = 30;

    /// <summary>Inactivity that ends a browsing session. The VisitorId survives; the SessionId does not.</summary>
    public int SessionIdleMinutes { get; set; } = 30;

    public int VisitorCookieDays { get; set; } = 365;

    /// <summary>
    /// Ceiling on a single event's reported active time. The browser is not trusted: a heartbeat
    /// claiming an hour is either a bug or an attempt to poison the numbers, and is clamped either way.
    /// </summary>
    public int MaximumActiveSecondsPerEvent { get; set; } = 300;

    public int MaximumPathLength { get; set; } = 500;

    /// <summary>
    /// Paths that are never meaningful activity. Matched case-insensitively as a prefix, alongside a
    /// static-file-extension check, so assets and framework chatter stay out of the events table.
    /// </summary>
    public string[] IgnoredPathPrefixes { get; set; } =
    [
        "/api/",
        "/_next/",
        "/static/",
        "/assets/",
        "/favicon",
        "/robots.txt",
        "/sitemap.xml"
    ];
}
