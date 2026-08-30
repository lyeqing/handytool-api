namespace handytool_api.Security;

/// <summary>
/// Where a request came from, as far as we are willing to believe it.
///
/// By the time this runs, <c>UseForwardedHeaders</c> has already decided whether to honour
/// <c>X-Forwarded-For</c> - and it only does so for proxies listed in configuration. So
/// <see cref="RemoteIpAddress"/> is either the real client (behind a configured proxy) or the
/// immediate socket peer (everywhere else). It is never a value a caller chose for itself.
/// </summary>
public static class ClientAddress
{
    /// <summary>
    /// Used when there is no address at all - in-process test hosts, mostly. Everything unknown
    /// shares one rate-limit bucket, which is the conservative way round.
    /// </summary>
    public const string Unknown = "unknown";

    public static string RemoteIpAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? Unknown;

    /// <summary>
    /// The rate-limit partition key.
    ///
    /// A signed-in caller is keyed on their stable user id, so their limit follows them across
    /// networks and devices rather than being shared with everyone behind the same office NAT. The
    /// session token is never used for this: it rotates on every sign-in, and a rotating key means an
    /// attacker can reset their own limit at will.
    /// </summary>
    public static string RateLimitPartition(HttpContext httpContext) =>
        CurrentUser.TryGetUserId(httpContext, out var userId)
            ? $"user:{userId}"
            : $"ip:{RemoteIpAddress(httpContext)}";

    /// <summary>
    /// IP only, for endpoints reached before anyone is authenticated. Sign-in cannot key on a user
    /// id - working out which user is being claimed is the very thing being protected.
    /// </summary>
    public static string IpPartition(HttpContext httpContext) => $"ip:{RemoteIpAddress(httpContext)}";
}
