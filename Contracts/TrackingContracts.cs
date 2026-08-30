using handytool_api.Models;

namespace handytool_api.Contracts;

// ---------- Requests ----------

/// <summary>
/// What the browser sends to <c>POST /api/track</c>.
///
/// Note what is absent: no visitor id, no session id, no user id. All three are resolved server-side
/// from the HttpOnly cookie and the bearer token. A client that could name them could file events
/// against another visitor or another account.
/// </summary>
public sealed record TrackEventRequest(
    string EventType,
    string Path,
    // Seconds the tab was actually visible since the last event. Clamped server-side.
    int? ActiveSeconds = null,
    string? Referrer = null,
    // The locale the page was rendered in. The browser knows this exactly, but it is still validated
    // against the supported list - an unrecognised tag is dropped rather than stored.
    string? Language = null);

// ---------- Responses ----------

/// <summary>
/// Deliberately says nothing. Echoing the visitor or session id back would defeat the point of an
/// HttpOnly cookie - any script on the page could then read the identity out of the response.
/// </summary>
public sealed record TrackAcceptedResponse(bool Recorded);

/// <summary>
/// The handful of settings the browser tracker needs. Served by the API so the heartbeat interval is
/// configured in one place rather than duplicated into the frontend.
/// </summary>
public sealed record TrackingConfigResponse(int HeartbeatSeconds, int MaximumActiveSecondsPerEvent);

/// <summary>One event on a user timeline, exactly as it was recorded.</summary>
public sealed record ActivityEventResponse(
    long Id,
    Guid ClientId,
    ClientType ClientType,
    Guid SessionId,
    // Null for events from before this visitor signed in. Never backfilled.
    long? UserId,
    string EventType,
    string Path,
    DateTime Timestamp,
    int? ActiveSeconds,
    string? Language,
    string? Referrer)
{
    public static ActivityEventResponse From(AnalyticsEvent analyticsEvent) => new(
        analyticsEvent.Id,
        analyticsEvent.ClientId,
        analyticsEvent.ClientType,
        analyticsEvent.SessionId,
        analyticsEvent.UserId,
        analyticsEvent.EventType,
        analyticsEvent.Path,
        analyticsEvent.Timestamp,
        analyticsEvent.ActiveSeconds,
        analyticsEvent.Language,
        analyticsEvent.Referrer);
}

/// <summary>A browser or device known to belong to the user, and when we found that out.</summary>
public sealed record LinkedClientResponse(
    Guid ClientId,
    ClientType ClientType,
    DateTime FirstIdentifiedAt,
    DateTime LastIdentifiedAt);

/// <summary>
/// Everything associated with one account: the events recorded against the account directly, plus the
/// anonymous events from every browser and device now known to be theirs, in one chronological list.
/// </summary>
public sealed record UserActivityResponse(
    long UserId,
    IReadOnlyList<LinkedClientResponse> Clients,
    IReadOnlyList<ActivityEventResponse> Events,
    int Skip,
    int Take,
    long Total);

/// <summary>Active time per page, rolled up from page views and heartbeats.</summary>
public sealed record PageActiveTimeResponse(
    string Path,
    int ActiveSeconds,
    int Views);
