using System.Security.Claims;
using System.Text.Encodings.Web;
using handytool_api.Configuration;
using handytool_api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace handytool_api.Security;

public sealed class SessionTokenAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// Resolves <c>Authorization: Bearer &lt;opaque token&gt;</c> to a stable user id.
///
/// The flow is deliberately boring: hash the presented token, look the hash up once on its unique
/// index, reject it if the session is revoked, expired or belongs to a deactivated account, and put
/// the session's UserId on the principal. Nothing is parsed out of the token itself, because there
/// is nothing in it - it is random bytes.
/// </summary>
public sealed class SessionTokenAuthenticationHandler
    : AuthenticationHandler<SessionTokenAuthenticationOptions>
{
    public const string SchemeName = "SessionToken";

    /// <summary>Which device is calling, as opposed to which account - needed by "log out this device".</summary>
    public const string SessionIdClaimType = "session_id";

    private const string BearerPrefix = "Bearer ";

    private readonly HandyToolDbContext _db;
    private readonly AuthOptions _authOptions;

    public SessionTokenAuthenticationHandler(
        IOptionsMonitor<SessionTokenAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        HandyToolDbContext db,
        IOptions<AuthOptions> authOptions)
        : base(options, logger, encoder)
    {
        _db = db;
        _authOptions = authOptions.Value;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No credential is not a failure. /api/track is anonymous-capable and must stay that way, so
        // an absent token means "no result", leaving the caller anonymous rather than rejected.
        if (!TryReadToken(out var token))
        {
            return AuthenticateResult.NoResult();
        }

        var tokenHash = SessionToken.Hash(token);
        var now = DateTime.UtcNow;

        var session = await _db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, Context.RequestAborted);

        // One message for every rejection reason. Telling a caller whether a token was unknown,
        // revoked or merely expired is free reconnaissance.
        if (session is null || !session.IsActiveAt(now) || !session.User.IsActive)
        {
            return AuthenticateResult.Fail("The session token is not valid.");
        }

        // Sliding "last used", written at most once per touch window rather than once per request.
        if (session.LastUsedDate < now.AddMinutes(-_authOptions.SessionTouchMinutes))
        {
            session.LastUsedDate = now;
            await _db.SaveChangesAsync(Context.RequestAborted);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(SessionIdClaimType, session.Id.ToString())
        };

        // Carried on the principal because the account row is already loaded here. Without it every
        // request that wants the user's language preference would cost a second query.
        if (session.User.PreferredLanguage is { Length: > 0 } preferredLanguage)
        {
            claims.Add(new Claim(RequestLanguage.PreferredLanguageClaimType, preferredLanguage));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    /// <summary>
    /// One credential, two transports. The header wins because a native app has no cookie jar and an
    /// explicit token should never be second-guessed; the cookie covers the browser, where holding a
    /// token in JavaScript would put it one XSS bug away from being stolen.
    /// </summary>
    private bool TryReadToken(out string token)
    {
        token = string.Empty;

        if (Request.Headers.TryGetValue("Authorization", out var header))
        {
            var raw = header.ToString();

            if (raw.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                token = raw[BearerPrefix.Length..].Trim();
            }
        }

        if (token.Length == 0 && SessionCookie.TryRead(Context, out var cookieToken))
        {
            token = cookieToken;
        }

        return token.Length > 0;
    }
}
