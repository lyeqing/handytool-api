namespace handytool_api.Security;

/// <summary>
/// Temporary ownership resolution seam.
///
/// The platform has no authentication yet, so ownership is read from the <c>X-Owner-Id</c> header.
/// The important property today is that <c>OwnerId</c> is NEVER read from a request body, so wiring
/// real authentication later means changing only this file: resolve the owner from the authenticated
/// principal (for example a "sub"/tenant claim) instead of the header.
/// </summary>
public static class CurrentOwner
{
    public const string HeaderName = "X-Owner-Id";

    public static bool TryGetOwnerId(HttpContext httpContext, out long ownerId)
    {
        ownerId = 0;

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var raw))
        {
            return false;
        }

        return long.TryParse(raw.ToString(), out ownerId) && ownerId > 0;
    }
}
