using handytool_api.Configuration;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace handytool_api.Services;

/// <summary>Why an event was not written, when it was not.</summary>
public enum TrackingRejection
{
    None = 0,

    /// <summary>A static asset, a framework request, or something else that is not website activity.</summary>
    IgnoredPath,

    MissingEventType,

    /// <summary>An event type only the API is allowed to write, such as <c>login</c>.</summary>
    ReservedEventType
}

public sealed record TrackingOutcome(
    TrackingRejection Rejection,
    TrackingIdentity? Identity = null,
    AnalyticsEvent? Event = null)
{
    public bool Recorded => Rejection == TrackingRejection.None;
}

/// <summary>
/// Writes analytics events, and only analytics events.
///
/// Two rules hold everywhere in this class. First, an event records the identity that was true when
/// it happened: a page view made while anonymous keeps <see cref="AnalyticsEvent.UserId"/> null
/// forever, even once we learn who the visitor was. Second, learning who a visitor is means adding a
/// <see cref="TrackingClientUser"/> row, never editing history.
/// </summary>
public sealed class AnalyticsTracker
{
    private readonly HandyToolDbContext _db;
    private readonly TrackingIdentityResolver _identityResolver;
    private readonly TrackingOptions _options;
    private readonly LocalizationOptions _localization;

    public AnalyticsTracker(
        HandyToolDbContext db,
        TrackingIdentityResolver identityResolver,
        IOptions<TrackingOptions> options,
        IOptions<LocalizationOptions> localization)
    {
        _db = db;
        _identityResolver = identityResolver;
        _options = options.Value;
        _localization = localization.Value;
    }

    /// <summary>
    /// Records one event sent by a client. Identity comes from the cookie and the bearer token;
    /// the body contributes only what happened, never who it happened to.
    /// </summary>
    public async Task<TrackingOutcome> RecordAsync(
        HttpContext httpContext,
        string? eventType,
        string? path,
        int? activeSeconds,
        string? referrer,
        string? language,
        CancellationToken cancellationToken)
    {
        var normalisedType = (eventType ?? string.Empty).Trim();

        if (normalisedType.Length == 0)
        {
            return new TrackingOutcome(TrackingRejection.MissingEventType);
        }

        if (AnalyticsEventTypes.ServerWritten.Contains(normalisedType, StringComparer.OrdinalIgnoreCase))
        {
            // login/logout are written by the auth endpoints, where the transition is actually known.
            // Letting a client post them would make the timeline lie.
            return new TrackingOutcome(TrackingRejection.ReservedEventType);
        }

        var normalisedPath = TrackingRules.NormalisePath(path, _options.MaximumPathLength);

        // /zh-Hans/tools and /en/tools are one page read in two languages. The prefix moves into the
        // event's Language so per-page reports do not split in half - and the URL itself is the most
        // reliable statement of which language was actually rendered, so it wins over the body.
        var (pagePath, pathLanguage) = TrackingRules.SplitLanguage(
            normalisedPath, _localization.SupportedLanguages);

        if (!TrackingRules.ShouldTrackPath(pagePath, _options.IgnoredPathPrefixes))
        {
            return new TrackingOutcome(TrackingRejection.IgnoredPath);
        }

        // Only page views refresh the cookie; heartbeats are far too frequent to justify it.
        var refreshCookie = string.Equals(normalisedType, AnalyticsEventTypes.PageView, StringComparison.OrdinalIgnoreCase);

        var identity = await _identityResolver.ResolveAsync(httpContext, refreshCookie, cancellationToken);

        var analyticsEvent = Append(
            identity, normalisedType, pagePath, activeSeconds, referrer, pathLanguage ?? language, httpContext);

        await LinkClientToUserAsync(identity, cancellationToken);

        // One round trip for the whole request: new client, new session, the event, and the link.
        await _db.SaveChangesAsync(cancellationToken);

        return new TrackingOutcome(TrackingRejection.None, identity, analyticsEvent);
    }

    /// <summary>
    /// Records an event the API itself knows about - <c>login</c> and <c>logout</c>. Called after the
    /// authentication decision, so the event carries the identity as it stands on the far side of it.
    /// </summary>
    public async Task<TrackingIdentity> RecordServerEventAsync(
        HttpContext httpContext,
        string eventType,
        long? userId,
        CancellationToken cancellationToken)
    {
        var resolved = await _identityResolver.ResolveAsync(httpContext, refreshCookie: true, cancellationToken);

        // On login the principal is still anonymous - the token was only just minted - so the caller
        // passes the user id it just established.
        var identity = resolved with { UserId = userId ?? resolved.UserId };

        Append(identity, eventType, "/", activeSeconds: null, referrer: null, language: null, httpContext);

        await LinkClientToUserAsync(identity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return identity;
    }

    /// <summary>
    /// "This browser belongs to this account", written the first time we see the pair and never
    /// again. Historical anonymous events are left exactly as they were: this row is how they are
    /// found later, and is why they do not need rewriting.
    /// </summary>
    private async Task LinkClientToUserAsync(TrackingIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.UserId is not { } userId)
        {
            return;
        }

        var link = await _db.TrackingClientUsers
            .FirstOrDefaultAsync(l => l.ClientId == identity.ClientId && l.UserId == userId, cancellationToken);

        var now = DateTime.UtcNow;

        if (link is null)
        {
            _db.TrackingClientUsers.Add(new TrackingClientUser
            {
                ClientId = identity.ClientId,
                UserId = userId,
                FirstIdentifiedAt = now,
                LastIdentifiedAt = now
            });
        }
        else
        {
            link.LastIdentifiedAt = now;
        }
    }

    private AnalyticsEvent Append(
        TrackingIdentity identity,
        string eventType,
        string path,
        int? activeSeconds,
        string? referrer,
        string? language,
        HttpContext httpContext)
    {
        var analyticsEvent = new AnalyticsEvent
        {
            ClientId = identity.ClientId,
            ClientType = identity.ClientType,
            SessionId = identity.SessionId,
            UserId = identity.UserId,
            EventType = TrackingRules.Truncate(eventType, 50)!,
            Path = path,
            Timestamp = DateTime.UtcNow,
            ActiveSeconds = TrackingRules.ClampActiveSeconds(activeSeconds, _options.MaximumActiveSecondsPerEvent),

            // Resolved rather than trusted: an unsupported tag from the body is discarded and the
            // usual chain - account preference, then Accept-Language - answers instead.
            Language = RequestLanguage.Resolve(httpContext, _localization, language),
            Referrer = TrackingRules.Truncate(TrackingRules.StripQuery(referrer), 1000),
            UserAgent = TrackingRules.Truncate(httpContext.Request.Headers.UserAgent.ToString(), 512)
        };

        _db.AnalyticsEvents.Add(analyticsEvent);
        return analyticsEvent;
    }
}
