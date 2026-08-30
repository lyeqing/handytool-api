namespace handytool_api.Models;

/// <summary>
/// One period of browsing - the analytics session. This is NOT <see cref="UserSession"/>: a visitor
/// who never signs in still has these, and a signed-in visitor accumulates a new one every time they
/// come back after being idle. The <see cref="ClientId"/> stays the same across all of them.
/// </summary>
public class TrackingSession
{
    /// <summary>The SessionId carried on every event of this browsing period.</summary>
    public Guid Id { get; set; }

    public Guid ClientId { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Rolled forward by each tracked event. A gap wider than the idle timeout starts a new session.</summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>Set when a session is explicitly closed. Idle sessions simply stop being extended.</summary>
    public DateTime? EndedAt { get; set; }

    public TrackingClient Client { get; set; } = null!;
}
