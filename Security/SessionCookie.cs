using handytool_api.Configuration;

namespace handytool_api.Security;

/// <summary>
/// The web transport for the session token.
///
/// This is not a second authentication mechanism. It is the same opaque token, hashed the same way
/// and resolved against the same <see cref="Models.UserSession"/> row - only carried in a cookie
/// instead of a header, because a browser cannot be trusted to hold a credential in JavaScript and
/// because server-rendered pages need it on the very first request. Native apps skip this entirely
/// and send <c>Authorization: Bearer</c>.
///
/// HttpOnly keeps it away from page scripts, so an XSS bug cannot walk off with a login.
/// <c>SameSite=Lax</c> is what stops another site posting to our API with the user's credential
/// attached: Lax withholds the cookie on cross-site POST, which is the CSRF defence for every
/// state-changing endpoint here.
/// </summary>
public static class SessionCookie
{
    public const string Name = "session_token";

    public static bool TryRead(HttpContext httpContext, out string token)
    {
        token = string.Empty;

        if (!httpContext.Request.Cookies.TryGetValue(Name, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        token = raw.Trim();
        return true;
    }

    public static void Write(HttpContext httpContext, string token, DateTime expiresUtc, bool secure)
    {
        var options = Options(secure);

        // Matches the session row's own expiry, so the browser stops sending a credential the API
        // would reject anyway.
        options.Expires = new DateTimeOffset(expiresUtc, TimeSpan.Zero);

        httpContext.Response.Cookies.Append(Name, token, options);
    }

    /// <summary>
    /// Clears the browser's copy on sign-out. The session row is revoked either way - deleting the
    /// cookie is a courtesy, not the security boundary.
    /// </summary>
    public static void Delete(HttpContext httpContext, bool secure) =>
        httpContext.Response.Cookies.Delete(Name, Options(secure));

    private static CookieOptions Options(bool secure) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true
    };
}
