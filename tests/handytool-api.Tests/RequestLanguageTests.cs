using System.Security.Claims;
using handytool_api.Configuration;
using handytool_api.Security;
using Microsoft.AspNetCore.Http;

namespace handytool_api.Tests;

public class RequestLanguageTests
{
    private static readonly LocalizationOptions Options = new();

    private static HttpContext Context(
        string? acceptLanguage = null,
        string? preferredLanguage = null,
        string? queryLanguage = null)
    {
        var httpContext = new DefaultHttpContext();

        if (acceptLanguage is not null)
        {
            httpContext.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        if (queryLanguage is not null)
        {
            httpContext.Request.QueryString = new QueryString($"?{RequestLanguage.QueryParameterName}={queryLanguage}");
        }

        if (preferredLanguage is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(RequestLanguage.PreferredLanguageClaimType, preferredLanguage)],
                "test"));
        }

        return httpContext;
    }

    [Fact]
    public void Falls_back_to_the_default_when_nothing_says_otherwise()
    {
        Assert.Equal("en", RequestLanguage.Resolve(Context(), Options));
    }

    [Fact]
    public void An_explicit_choice_wins_over_everything()
    {
        // Somebody who just clicked a language switcher means it right now, whatever their account
        // or their browser says.
        var context = Context(acceptLanguage: "en-AU", preferredLanguage: "en");

        Assert.Equal("zh-Hans", RequestLanguage.Resolve(context, Options, "zh-Hans"));
    }

    [Fact]
    public void The_query_parameter_beats_the_account_preference()
    {
        var context = Context(preferredLanguage: "en", queryLanguage: "zh-Hans");

        Assert.Equal("zh-Hans", RequestLanguage.Resolve(context, Options));
    }

    [Fact]
    public void The_account_preference_beats_the_browser()
    {
        // This is what makes the choice follow someone to a borrowed laptop.
        var context = Context(acceptLanguage: "en-AU,en;q=0.9", preferredLanguage: "zh-Hans");

        Assert.Equal("zh-Hans", RequestLanguage.Resolve(context, Options));
    }

    [Fact]
    public void The_browser_is_used_when_the_account_has_no_preference()
    {
        Assert.Equal("zh-Hans", RequestLanguage.Resolve(Context(acceptLanguage: "zh-Hans"), Options));
    }

    [Theory]
    [InlineData("zh-CN,zh;q=0.9,en;q=0.8")]
    [InlineData("zh")]
    [InlineData("zh-TW")]
    public void A_related_chinese_tag_still_finds_simplified_chinese(string acceptLanguage)
    {
        // A browser asking for zh-CN should not fall all the way back to English just because the
        // supported tag is spelled zh-Hans.
        Assert.Equal("zh-Hans", RequestLanguage.Resolve(Context(acceptLanguage), Options));
    }

    [Fact]
    public void Quality_order_is_respected_over_header_order()
    {
        Assert.Equal("zh-Hans", RequestLanguage.Resolve(Context("en;q=0.2,zh-Hans;q=0.9"), Options));
    }

    [Fact]
    public void A_zero_quality_tag_is_a_refusal_not_a_preference()
    {
        Assert.Equal("zh-Hans", RequestLanguage.Resolve(Context("en;q=0,zh-Hans;q=0.5"), Options));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr")]
    [InlineData("klingon")]
    [InlineData("")]
    public void An_unsupported_tag_falls_through_to_the_default(string acceptLanguage)
    {
        Assert.Equal("en", RequestLanguage.Resolve(Context(acceptLanguage), Options));
    }

    [Fact]
    public void An_unsupported_explicit_choice_is_ignored_rather_than_honoured()
    {
        // The important one. An unrecognised tag must never be returned, or it would end up as a key
        // in a translations map that nothing can ever read back.
        var context = Context(acceptLanguage: "zh-Hans");

        Assert.Equal("zh-Hans", RequestLanguage.Resolve(context, Options, "de-DE"));
    }
}

public class LocalizationOptionsTests
{
    private static readonly LocalizationOptions Options = new();

    [Theory]
    [InlineData("en")]
    [InlineData("zh-Hans")]
    public void Configured_languages_are_supported(string language)
    {
        Assert.True(Options.IsSupported(language));
    }

    [Theory]
    [InlineData("ZH-HANS", "zh-Hans")]
    [InlineData("zh-hans", "zh-Hans")]
    [InlineData("EN", "en")]
    public void Casing_is_normalised_to_the_configured_spelling(string input, string expected)
    {
        // Otherwise "ZH-HANS" and "zh-Hans" would become two separate keys in a translations map and
        // half the translations would silently stop resolving.
        Assert.Equal(expected, Options.Normalise(input));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("zh-Hant")]
    [InlineData(null)]
    public void Anything_unsupported_normalises_to_null(string? input)
    {
        Assert.Null(Options.Normalise(input));
        Assert.False(Options.IsSupported(input));
    }
}
