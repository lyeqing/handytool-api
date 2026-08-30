namespace handytool_api.Models;

/// <summary>
/// One thing that happened, recorded exactly as it was true at the time.
///
/// Rows here are immutable: an event written while the visitor was anonymous keeps
/// <see cref="UserId"/> null forever, even after we learn who the visitor was. The link lives in
/// <see cref="TrackingClientUser"/> instead, so history is never rewritten.
/// </summary>
public class AnalyticsEvent
{
    public long Id { get; set; }

    /// <summary>The VisitorId for web clients; the installation id for a future native client.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Denormalised from the client so analytics queries can split web from native without a join.</summary>
    public ClientType ClientType { get; set; } = ClientType.Web;

    public Guid SessionId { get; set; }

    /// <summary>
    /// The authenticated account at the moment of the event, or null if the visitor was anonymous.
    /// Resolved server-side from the bearer token's session - never accepted from the request body.
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>One of <see cref="AnalyticsEventTypes"/>, or any future snake_case name.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Path only - query strings are stripped before storage so they cannot smuggle in personal data.</summary>
    public string Path { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Seconds the browser measured the page as actually visible. Sent by the client because only the
    /// client can see tab visibility; clamped server-side so a bad actor cannot inflate it.
    /// </summary>
    public int? ActiveSeconds { get; set; }

    /// <summary>
    /// The language the page was rendered in. Recorded at the time because, like everything else
    /// here, it can never be worked out afterwards: an event written before this column existed has
    /// no way of knowing, and history is not rewritten.
    /// </summary>
    public string? Language { get; set; }

    public string? Referrer { get; set; }

    public string? UserAgent { get; set; }

    public TrackingClient Client { get; set; } = null!;

    public TrackingSession Session { get; set; } = null!;
}
