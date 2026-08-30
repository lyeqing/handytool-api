using System.Text.Json;
using handytool_api.Configuration;
using handytool_api.Localization;

namespace handytool_api.Tests;

public class LocalizedTextTests
{
    private static readonly LocalizationOptions Options = new();

    private static JsonDocument Map(string json) => JsonDocument.Parse(json);

    [Fact]
    public void A_translation_is_used_when_one_exists()
    {
        var translations = Map("""{"zh-Hans": "房产检查"}""");

        Assert.Equal("房产检查", LocalizedText.Resolve("Property Inspection", translations, "zh-Hans"));
    }

    [Fact]
    public void The_canonical_text_is_used_for_the_default_language()
    {
        var translations = Map("""{"zh-Hans": "房产检查"}""");

        Assert.Equal("Property Inspection", LocalizedText.Resolve("Property Inspection", translations, "en"));
    }

    [Fact]
    public void An_untranslated_label_falls_back_rather_than_rendering_blank()
    {
        // The whole reason the canonical column is kept alongside the map. A missing translation
        // shows the original, never an empty label.
        Assert.Equal("Property Inspection", LocalizedText.Resolve("Property Inspection", LocalizedText.Empty(), "zh-Hans"));
    }

    [Theory]
    [InlineData("""{"zh-Hans": ""}""")]
    [InlineData("""{"zh-Hans": null}""")]
    [InlineData("""{"zh-Hans": 42}""")]
    [InlineData("""{"zh-Hans": {"nested": "no"}}""")]
    public void A_useless_translation_value_falls_back_too(string json)
    {
        Assert.Equal("Property Inspection", LocalizedText.Resolve("Property Inspection", Map(json), "zh-Hans"));
    }

    [Fact]
    public void A_null_map_is_safe()
    {
        Assert.Equal("Property Inspection", LocalizedText.Resolve("Property Inspection", null, "zh-Hans"));
    }

    // ---------- Sanitise ----------

    [Fact]
    public void A_supported_translation_is_kept()
    {
        var result = LocalizedText.Sanitise(new Dictionary<string, string> { ["zh-Hans"] = "水损" }, Options, 200);

        Assert.Equal("水损", LocalizedText.Resolve("Water Damage", result, "zh-Hans"));
    }

    [Fact]
    public void An_unsupported_language_is_dropped()
    {
        // Storing it would fill the map with keys nothing can ever read back.
        var result = LocalizedText.Sanitise(
            new Dictionary<string, string> { ["de-DE"] = "Wasserschaden", ["zh-Hans"] = "水损" },
            Options,
            200);

        var stored = LocalizedText.ToDictionary(result);

        Assert.Single(stored);
        Assert.True(stored.ContainsKey("zh-Hans"));
    }

    [Fact]
    public void Language_keys_are_normalised_so_one_language_cannot_become_two()
    {
        var result = LocalizedText.Sanitise(new Dictionary<string, string> { ["ZH-HANS"] = "水损" }, Options, 200);

        Assert.Equal("水损", LocalizedText.Resolve("Water Damage", result, "zh-Hans"));
    }

    [Fact]
    public void A_translation_is_held_to_the_same_length_limit_as_its_column()
    {
        // Without this, the 200-character limit on Label would be bypassed simply by putting the
        // text in the translation instead.
        var oversized = new string('水', 500);

        var result = LocalizedText.Sanitise(new Dictionary<string, string> { ["zh-Hans"] = oversized }, Options, 200);

        Assert.Equal(200, LocalizedText.Resolve("Water Damage", result, "zh-Hans").Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_translations_are_dropped_not_stored(string value)
    {
        var result = LocalizedText.Sanitise(new Dictionary<string, string> { ["zh-Hans"] = value }, Options, 200);

        Assert.Empty(LocalizedText.ToDictionary(result));
        Assert.Equal("Water Damage", LocalizedText.Resolve("Water Damage", result, "zh-Hans"));
    }

    [Fact]
    public void Values_are_trimmed()
    {
        var result = LocalizedText.Sanitise(new Dictionary<string, string> { ["zh-Hans"] = "  水损  " }, Options, 200);

        Assert.Equal("水损", LocalizedText.Resolve("Water Damage", result, "zh-Hans"));
    }

    [Fact]
    public void A_null_or_empty_map_produces_an_empty_json_object()
    {
        // The column is NOT NULL with a {} default and an is-object check constraint, so this has to
        // be a real empty object rather than null or a JSON null.
        Assert.Equal(JsonValueKind.Object, LocalizedText.Sanitise(null, Options, 200).RootElement.ValueKind);
        Assert.Equal(
            JsonValueKind.Object,
            LocalizedText.Sanitise(new Dictionary<string, string>(), Options, 200).RootElement.ValueKind);
    }

    [Fact]
    public void Chinese_survives_the_json_round_trip_unescaped_or_not()
    {
        var result = LocalizedText.Sanitise(new Dictionary<string, string> { ["zh-Hans"] = "房产检查" }, Options, 200);

        Assert.Equal("房产检查", LocalizedText.Resolve("Property Inspection", result, "zh-Hans"));
        Assert.Equal(4, LocalizedText.Resolve("Property Inspection", result, "zh-Hans").Length);
    }

    // ---------- MissingLanguages ----------

    [Fact]
    public void Missing_languages_lists_what_still_needs_translating()
    {
        Assert.Equal(["zh-Hans"], LocalizedText.MissingLanguages(LocalizedText.Empty(), Options));
    }

    [Fact]
    public void Nothing_is_missing_once_every_language_is_present()
    {
        var complete = LocalizedText.Sanitise(new Dictionary<string, string> { ["zh-Hans"] = "房产检查" }, Options, 200);

        Assert.Empty(LocalizedText.MissingLanguages(complete, Options));
    }

    [Fact]
    public void The_default_language_never_counts_as_missing()
    {
        // It lives in the canonical column, not the map.
        Assert.DoesNotContain("en", LocalizedText.MissingLanguages(LocalizedText.Empty(), Options));
    }
}
