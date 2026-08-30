namespace handytool_api.Configuration;

/// <summary>
/// Which proxies are allowed to tell us who the client is. Bound from the "ForwardedHeaders" section.
///
/// Empty by default, and that default is the safe one. With nothing trusted, ASP.NET Core ignores
/// <c>X-Forwarded-For</c> entirely and every IP-based limit sees the socket address - wrong behind a
/// proxy, but merely wrong. Trusting the header unconditionally would be worse: any caller could
/// invent a new address per request and give themselves an unlimited number of rate-limit buckets.
/// </summary>
public sealed class ForwardedHeaderOptions
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Individual proxy addresses, for example "10.0.0.4" or "::1".</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Proxy networks in CIDR form, for example "10.0.0.0/8".</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// How many hops to walk back through X-Forwarded-For. One proxy means one - raising this without
    /// actually having that many trusted hops lets a client prepend its own value and be believed.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;

    public bool HasTrustedProxies => KnownProxies.Length > 0 || KnownNetworks.Length > 0;
}
