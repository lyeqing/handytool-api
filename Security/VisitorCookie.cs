using handytool_api.Configuration;

namespace handytool_api.Security;

/// <summary>
/// The <c>visitor_id</c> cookie: one opaque GUID naming a browser, and nothing else.
///
/// It is HttpOnly, so page scripts cannot read it, copy it into a URL or ship it to a third party -
/// the browser simply attaches it to same-origin <c>/api/*</c> calls. In production the website and
/// this API share the origin <c>https://handytool.org</c> behind a reverse proxy, which is what makes
/// a plain first-party <c>SameSite=Lax</c> cookie the right tool and CORS unnecessary.
///
/// It never contains a name, an email, an account id or a token: those would turn a browser
/// identifier into personal data sitting in plaintext on the client.
/// </summary>
public static class VisitorCookie
{
    public const string Name = "visitor_id";

    public static bool TryRead(HttpContext httpContext, out Guid visitorId)
    {
        visitorId = Guid.Empty;

        return httpContext.Request.Cookies.TryGetValue(Name, out var raw)
            && Guid.TryParse(raw, out visitorId)
            && visitorId != Guid.Empty;
    }

    public static void Write(HttpContext httpContext, Guid visitorId, TrackingOptions options, bool secure)
    {
        httpContext.Response.Cookies.Append(Name, visitorId.ToString(), new CookieOptions
        {
            HttpOnly = true,

            // Off in Development only, where the site is plain http://localhost and a Secure cookie
            // would silently never be stored.
            Secure = secure,

            // Lax, not None: everything that needs this cookie is same-origin, and None would hand it
            // to any site that can make a request to us.
            SameSite = SameSiteMode.Lax,

            // Root path, not /api - the cookie belongs to the whole origin, and the reverse proxy
            // means the website and the API are two paths on one site.
            Path = "/",

            MaxAge = TimeSpan.FromDays(options.VisitorCookieDays),
            IsEssential = true
        });
    }
}
