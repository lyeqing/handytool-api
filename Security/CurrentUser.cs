using System.Security.Claims;

namespace handytool_api.Security;

/// <summary>
/// The single place the authenticated account identity is read.
///
/// The value comes from the claim the authentication handler minted after resolving a bearer token
/// to a <see cref="Models.UserSession"/>, so it is always a stable <see cref="Models.UserAccount.Id"/>
/// and never a token, a cookie or anything a client sent in a body.
/// </summary>
public static class CurrentUser
{
    public static bool TryGetUserId(HttpContext httpContext, out long userId)
    {
        userId = 0;

        var claim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return claim is not null && long.TryParse(claim, out userId) && userId > 0;
    }

    /// <summary>Null when the caller is anonymous - the shape analytics wants.</summary>
    public static long? GetUserIdOrNull(HttpContext httpContext) =>
        TryGetUserId(httpContext, out var userId) ? userId : null;

    /// <summary>The authenticated session's own id, so "log out this device" knows which row to revoke.</summary>
    public static bool TryGetSessionId(HttpContext httpContext, out Guid sessionId)
    {
        sessionId = Guid.Empty;

        var claim = httpContext.User.FindFirstValue(SessionTokenAuthenticationHandler.SessionIdClaimType);

        return claim is not null && Guid.TryParse(claim, out sessionId);
    }
}
