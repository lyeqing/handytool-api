using handytool_api.Configuration;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace handytool_api.Services;

/// <summary>
/// The three identities an event is filed under, kept strictly apart:
/// <list type="bullet">
///   <item><see cref="ClientId"/> - the browser or device. For the web this is the VisitorId from the
///   HttpOnly cookie. Survives login, logout and everything else.</item>
///   <item><see cref="SessionId"/> - one period of browsing. A new one starts after the idle window;
///   the ClientId does not change with it.</item>
///   <item><see cref="UserId"/> - the account, or null while anonymous. Resolved from the bearer
///   token's session, never from anything the client typed.</item>
/// </list>
/// </summary>
public sealed record TrackingIdentity(
    Guid ClientId,
    ClientType ClientType,
    Guid SessionId,
    long? UserId)
{
    public bool IsAuthenticated => UserId is not null;
}

/// <summary>
/// Works out which browser, which browsing session and which account a tracking request belongs to.
///
/// Everything here is decided server-side. The request body is never consulted for identity: a client
/// that could name its own VisitorId or UserId could file events against somebody else.
/// </summary>
public sealed class TrackingIdentityResolver
{
    /// <summary>Header a future native app sets, since it has no cookie jar. Ignored for web callers.</summary>
    public const string ClientTypeHeader = "X-Client-Type";

    /// <summary>The native app equivalent of the visitor cookie: a persistent installation id.</summary>
    public const string ClientIdHeader = "X-Client-Id";

    private readonly HandyToolDbContext _db;
    private readonly TrackingOptions _options;
    private readonly IWebHostEnvironment _environment;

    public TrackingIdentityResolver(
        HandyToolDbContext db,
        IOptions<TrackingOptions> options,
        IWebHostEnvironment environment)
    {
        _db = db;
        _options = options.Value;
        _environment = environment;
    }

    /// <summary>
    /// Resolves - and where necessary creates - the client and browsing session for this request.
    /// Nothing is saved here; the caller commits everything in one <c>SaveChanges</c> along with the
    /// event itself.
    /// </summary>
    public async Task<TrackingIdentity> ResolveAsync(
        HttpContext httpContext,
        bool refreshCookie,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var clientType = ReadClientType(httpContext);

        var (clientId, isNewCookie) = clientType == ClientType.Web
            ? ResolveWebVisitorId(httpContext)
            : (ResolveNativeClientId(httpContext), false);

        // A cookie is only useful to a browser, and only the browser flow mints one.
        if (clientType == ClientType.Web && (isNewCookie || refreshCookie))
        {
            // Re-issued on page views as well as on first sight, so a returning visitor keeps the same
            // identity instead of ageing out of a fixed one-year window. Heartbeats skip this, which
            // keeps Set-Cookie off the great majority of tracking responses.
            VisitorCookie.Write(httpContext, clientId, _options, secure: !_environment.IsDevelopment());
        }

        var client = await _db.TrackingClients
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

        if (client is null)
        {
            client = new TrackingClient
            {
                Id = clientId,
                ClientType = clientType,
                FirstSeenDate = now,
                LastSeenDate = now,
                CreatedDate = now
            };

            _db.TrackingClients.Add(client);
        }
        else
        {
            client.LastSeenDate = now;
        }

        var session = await ResolveSessionAsync(client, now, cancellationToken);

        return new TrackingIdentity(
            clientId,
            client.ClientType,
            session.Id,
            CurrentUser.GetUserIdOrNull(httpContext));
    }

    /// <summary>
    /// The current browsing session, or a fresh one if the last activity is older than the idle
    /// window. Either way the ClientId is untouched - a returning visitor is the same visitor.
    /// </summary>
    private async Task<TrackingSession> ResolveSessionAsync(
        TrackingClient client,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var cutoff = now.AddMinutes(-_options.SessionIdleMinutes);

        // Local first: within one request the session may have been added but not yet saved.
        var session = _db.ChangeTracker.Entries<TrackingSession>()
            .Select(e => e.Entity)
            .FirstOrDefault(s => s.ClientId == client.Id && s.EndedAt == null && s.LastActivityAt > cutoff);

        session ??= await _db.TrackingSessions
            .Where(s => s.ClientId == client.Id && s.EndedAt == null && s.LastActivityAt > cutoff)
            .OrderByDescending(s => s.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            session = new TrackingSession
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                StartedAt = now,
                LastActivityAt = now
            };

            _db.TrackingSessions.Add(session);
        }
        else
        {
            session.LastActivityAt = now;
        }

        return session;
    }

    /// <summary>
    /// Reads the visitor cookie, or mints a new opaque GUID. A cookie holding anything other than a
    /// GUID is treated as absent rather than trusted - it did not come from us.
    /// </summary>
    private static (Guid ClientId, bool IsNew) ResolveWebVisitorId(HttpContext httpContext) =>
        VisitorCookie.TryRead(httpContext, out var visitorId)
            ? (visitorId, false)
            : (Guid.NewGuid(), true);

    /// <summary>
    /// A native app supplies its own persistent installation id. It is an opaque client identifier in
    /// exactly the same sense as the web VisitorId - not an account, and not a credential.
    /// </summary>
    private static Guid ResolveNativeClientId(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue(ClientIdHeader, out var raw) && Guid.TryParse(raw, out var clientId)
            ? clientId
            : Guid.NewGuid();

    private static ClientType ReadClientType(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue(ClientTypeHeader, out var raw)
        && Enum.TryParse<ClientType>(raw.ToString(), ignoreCase: true, out var clientType)
            ? clientType
            : ClientType.Web;
}
