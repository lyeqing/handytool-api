using handytool_api.Configuration;
using handytool_api.Services;

namespace handytool_api.Tests;

public class TrackingRulesTests
{
    private static readonly TrackingOptions Options = new();

    private static bool ShouldTrack(string path) =>
        TrackingRules.ShouldTrackPath(Normalise(path), Options.IgnoredPathPrefixes);

    private static string Normalise(string? path) =>
        TrackingRules.NormalisePath(path, Options.MaximumPathLength);

    [Theory]
    [InlineData("/")]
    [InlineData("/tools")]
    [InlineData("/properties/123")]
    [InlineData("/schemas/4/records/new")]
    public void Real_pages_are_tracked(string path)
    {
        Assert.True(ShouldTrack(path));
    }

    [Theory]
    [InlineData("/_next/static/chunks/main.js")]
    [InlineData("/favicon.ico")]
    [InlineData("/static/logo.png")]
    [InlineData("/assets/app.css")]
    [InlineData("/robots.txt")]
    public void Static_assets_and_framework_paths_are_not_tracked(string path)
    {
        Assert.False(ShouldTrack(path));
    }

    [Fact]
    public void The_apis_own_routes_are_not_tracked()
    {
        // Otherwise every heartbeat would record a page view of the heartbeat endpoint.
        Assert.False(ShouldTrack("/api/track"));
        Assert.False(ShouldTrack("/api/auth/login"));
    }

    [Theory]
    [InlineData("/photo.PNG")]
    [InlineData("/bundle.JS")]
    public void Extension_matching_ignores_case(string path)
    {
        Assert.False(ShouldTrack(path));
    }

    [Fact]
    public void A_page_whose_name_contains_a_dot_is_still_a_page()
    {
        Assert.True(ShouldTrack("/tools/v1.2/overview"));
    }

    [Fact]
    public void An_empty_path_is_not_tracked()
    {
        Assert.False(ShouldTrack(""));
        Assert.False(ShouldTrack("   "));
    }

    [Fact]
    public void Query_strings_are_stripped_before_storage()
    {
        // Search terms, email addresses and password-reset tokens all live in query strings. None of
        // them belong in an analytics table.
        Assert.Equal("/search", Normalise("/search?q=louis@example.com"));
        Assert.Equal("/reset", Normalise("/reset?token=abc123"));
        Assert.Equal("/tools", Normalise("/tools#section"));
    }

    [Fact]
    public void An_absolute_url_is_reduced_to_its_path()
    {
        // A client cannot file events against somebody else's site.
        Assert.Equal("/tools", Normalise("https://evil.example/tools"));
    }

    [Fact]
    public void Paths_are_canonicalised()
    {
        Assert.Equal("/tools", Normalise("/tools/"));
        Assert.Equal("/tools", Normalise("tools"));
        Assert.Equal("/", Normalise("/"));
    }

    [Fact]
    public void An_over_long_path_is_truncated_rather_than_rejected()
    {
        var path = "/" + new string('a', Options.MaximumPathLength + 200);

        Assert.Equal(Options.MaximumPathLength, Normalise(path).Length);
    }

    [Fact]
    public void Reported_active_time_is_clamped()
    {
        // The browser measures active time because only it can see tab visibility - but it is still
        // a client, so a heartbeat claiming an hour is not taken at face value.
        Assert.Equal(30, TrackingRules.ClampActiveSeconds(30, Options.MaximumActiveSecondsPerEvent));
        Assert.Equal(Options.MaximumActiveSecondsPerEvent,
            TrackingRules.ClampActiveSeconds(9_999, Options.MaximumActiveSecondsPerEvent));
        Assert.Equal(0, TrackingRules.ClampActiveSeconds(-5, Options.MaximumActiveSecondsPerEvent));
    }

    [Fact]
    public void Absent_active_time_stays_absent()
    {
        // A page_view carries no active time yet. Null and zero mean different things.
        Assert.Null(TrackingRules.ClampActiveSeconds(null, Options.MaximumActiveSecondsPerEvent));
    }
}

public class TrackingPathLanguageTests
{
    private static readonly string[] Supported = ["en", "zh-Hans"];

    private static (string Path, string? Language) Split(string path) =>
        TrackingRules.SplitLanguage(path, Supported);

    [Fact]
    public void A_locale_prefix_is_lifted_out_of_the_path()
    {
        // The point of the whole exercise: both languages report against one page.
        Assert.Equal(("/tools", "en"), Split("/en/tools"));
        Assert.Equal(("/tools", "zh-Hans"), Split("/zh-Hans/tools"));
    }

    [Fact]
    public void A_locale_only_path_becomes_the_root()
    {
        Assert.Equal(("/", "en"), Split("/en"));
        Assert.Equal(("/", "zh-Hans"), Split("/zh-Hans"));
    }

    [Fact]
    public void Deep_paths_keep_everything_after_the_locale()
    {
        Assert.Equal(("/schemas/4/records/new", "zh-Hans"), Split("/zh-Hans/schemas/4/records/new"));
    }

    [Theory]
    [InlineData("/entries")]
    [InlineData("/english-lessons")]
    [InlineData("/energy")]
    public void A_page_that_merely_starts_with_a_locale_is_left_alone(string path)
    {
        // Only a whole segment counts. Prefix matching here would quietly turn /entries into /tries.
        Assert.Equal((path, null), Split(path));
    }

    [Fact]
    public void An_unsupported_locale_segment_is_left_in_the_path()
    {
        // If it is not a language we serve, it is just a page name.
        Assert.Equal(("/de/tools", null), Split("/de/tools"));
    }

    [Fact]
    public void The_bare_root_is_unchanged()
    {
        Assert.Equal(("/", null), Split("/"));
    }

    [Fact]
    public void Locale_matching_ignores_case()
    {
        Assert.Equal(("/tools", "zh-Hans"), Split("/ZH-HANS/tools"));
    }
}
