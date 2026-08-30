namespace handytool_api.Services;

/// <summary>
/// The pure decisions behind a tracked event: is this a real page, what is its canonical path, and
/// how much of the client's reported active time do we believe.
///
/// Kept free of the database and of HttpContext so the rules that decide what gets stored can be read
/// - and tested - on their own.
/// </summary>
public static class TrackingRules
{
    /// <summary>
    /// Extensions that are never a page. Belt and braces alongside the ignored prefixes: the browser
    /// tracker does not report assets at all, so anything arriving here is a misconfiguration or a
    /// script, and either way it does not belong in the events table.
    /// </summary>
    private static readonly string[] StaticFileExtensions =
    [
        ".js", ".mjs", ".css", ".map", ".ico", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp",
        ".avif", ".woff", ".woff2", ".ttf", ".otf", ".eot", ".mp4", ".webm", ".mp3", ".pdf", ".txt", ".xml"
    ];

    /// <summary>
    /// Assets, framework chatter and the API's own routes are not website activity. Filtering here as
    /// well as in the browser keeps the events table meaningful whatever a client decides to send.
    /// </summary>
    public static bool ShouldTrackPath(string path, IReadOnlyList<string> ignoredPrefixes)
    {
        if (path.Length == 0)
        {
            return false;
        }

        foreach (var prefix in ignoredPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var lastSegment = path[(path.LastIndexOf('/') + 1)..];
        var dot = lastSegment.LastIndexOf('.');

        return dot < 0
            || !StaticFileExtensions.Contains(lastSegment[dot..], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Splits a locale-prefixed path into the language and the page.
    ///
    /// The site serves /en/tools and /zh-Hans/tools, which are the same page read in two languages.
    /// Storing them as two different paths would split every per-page report in half - so the prefix
    /// is lifted out into the event's Language and the path is stored once. Reports then aggregate by
    /// page and can still be broken down by language on demand.
    ///
    /// Done server-side rather than in the browser so the future native apps, and anything else that
    /// posts a path, get the same treatment.
    /// </summary>
    public static (string Path, string? Language) SplitLanguage(string path, IReadOnlyList<string> supportedLanguages)
    {
        if (path.Length < 2 || path[0] != '/')
        {
            return (path, null);
        }

        var end = path.IndexOf('/', 1);
        var firstSegment = end < 0 ? path[1..] : path[1..end];

        var language = supportedLanguages.FirstOrDefault(
            supported => string.Equals(supported, firstSegment, StringComparison.OrdinalIgnoreCase));

        if (language is null)
        {
            // Only a whole segment that exactly matches a supported tag counts. A real page called
            // /entries must not lose its first three characters to the "en" locale.
            return (path, null);
        }

        var remainder = end < 0 ? "/" : path[end..];

        return (remainder.Length == 0 ? "/" : remainder, language);
    }

    /// <summary>
    /// Path only. The query string is dropped before anything is stored: search terms, email
    /// addresses and tokens all end up there, and none of them belong in an analytics table.
    /// </summary>
    public static string NormalisePath(string? path, int maximumLength)
    {
        var value = (path ?? string.Empty).Trim();

        if (value.Length == 0)
        {
            return string.Empty;
        }

        // An absolute URL is accepted but reduced to its path, so a client cannot file events under
        // somebody else's site.
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            value = absolute.AbsolutePath;
        }

        value = StripQuery(value)!;

        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        // Trailing slashes are noise: "/tools/" and "/tools" are one page.
        if (value.Length > 1)
        {
            value = value.TrimEnd('/');

            if (value.Length == 0)
            {
                value = "/";
            }
        }

        return Truncate(value, maximumLength)!;
    }

    /// <summary>
    /// The browser is the only thing that can see whether its tab is visible, so it reports active
    /// time - but it is still a client, so the number is capped. A heartbeat claiming an hour is
    /// either a bug or an attempt to poison the numbers.
    /// </summary>
    public static int? ClampActiveSeconds(int? activeSeconds, int maximum) =>
        activeSeconds is null ? null : Math.Clamp(activeSeconds.Value, 0, maximum);

    /// <summary>Query strings and fragments are removed rather than stored and forgotten about.</summary>
    public static string? StripQuery(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var cut = value.IndexOfAny(['?', '#']);
        return cut < 0 ? value : value[..cut];
    }

    public static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrEmpty(value)
            ? value
            : value.Length <= maximumLength ? value : value[..maximumLength];
}
