using System.Security.Claims;
using handytool_api.Configuration;

namespace handytool_api.Security;

/// <summary>
/// Works out which language to answer a request in, in one place.
///
/// The order matters. An explicit choice beats a stored preference, because someone who just clicked
/// a language switcher means it right now. A stored preference beats the browser, because it follows
/// them to a borrowed laptop. The browser beats the default, because it is usually right for a first
/// visit. And anything unrecognised falls through to the default rather than being honoured - an
/// unsupported tag must never reach a translations map and become a key nothing can read.
/// </summary>
public static class RequestLanguage
{
    /// <summary>Optional query parameter, for a link that names its own language.</summary>
    public const string QueryParameterName = "lang";

    /// <summary>
    /// Put on the principal at authentication time from the account row that was already loaded, so
    /// reading a user's preference costs nothing extra per request.
    /// </summary>
    public const string PreferredLanguageClaimType = "preferred_language";

    public static string Resolve(HttpContext httpContext, LocalizationOptions options, string? explicitLanguage = null)
    {
        // 1. Named outright by the caller.
        if (options.Normalise(explicitLanguage) is { } chosen)
        {
            return chosen;
        }

        if (httpContext.Request.Query.TryGetValue(QueryParameterName, out var queryValue)
            && options.Normalise(queryValue.ToString()) is { } fromQuery)
        {
            return fromQuery;
        }

        // 2. The signed-in account's saved preference.
        var claim = httpContext.User.FindFirstValue(PreferredLanguageClaimType);

        if (options.Normalise(claim) is { } fromAccount)
        {
            return fromAccount;
        }

        // 3. What the browser asked for.
        if (FromAcceptLanguage(httpContext, options) is { } fromBrowser)
        {
            return fromBrowser;
        }

        return options.DefaultLanguage;
    }

    /// <summary>
    /// Reads Accept-Language in quality order.
    ///
    /// Deliberately simple: each candidate is tried exactly and then by primary subtag, so a browser
    /// asking for "zh-CN" or plain "zh" lands on "zh-Hans" rather than falling back to English. Full
    /// RFC 4647 negotiation would buy nothing for a two-language site.
    /// </summary>
    private static string? FromAcceptLanguage(HttpContext httpContext, LocalizationOptions options)
    {
        var header = httpContext.Request.Headers.AcceptLanguage.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var candidates = header
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseEntry)
            .Where(entry => entry.Quality > 0)
            .OrderByDescending(entry => entry.Quality)
            .Select(entry => entry.Tag)
            .ToList();

        // One candidate at a time, exact then prefix, in quality order. Doing all the exact matches
        // first would be wrong: "zh-CN,zh;q=0.9,en;q=0.8" would match en exactly before trying zh as
        // a prefix, and hand English to somebody who plainly asked for Chinese.
        foreach (var candidate in candidates)
        {
            if (options.Normalise(candidate) is { } exact)
            {
                return exact;
            }

            var primary = candidate.Split('-')[0];

            var prefixed = options.SupportedLanguages.FirstOrDefault(supported =>
                string.Equals(supported, primary, StringComparison.OrdinalIgnoreCase)
                || supported.StartsWith(primary + "-", StringComparison.OrdinalIgnoreCase));

            if (prefixed is not null)
            {
                return prefixed;
            }
        }

        return null;
    }

    /// <summary>Splits "zh-CN;q=0.9" into its tag and weight. A missing weight means 1.0.</summary>
    private static (string Tag, double Quality) ParseEntry(string entry)
    {
        var parts = entry.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tag = parts[0];

        foreach (var part in parts.Skip(1))
        {
            if (part.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(part[2..], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var quality))
            {
                return (tag, quality);
            }
        }

        return (tag, 1.0);
    }
}
