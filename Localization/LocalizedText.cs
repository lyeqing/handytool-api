using System.Text.Json;
using handytool_api.Configuration;

namespace handytool_api.Localization;

/// <summary>
/// Resolving and validating the translation maps that sit alongside the canonical text columns.
///
/// The shape is deliberate: <c>Name</c> keeps holding the original string and
/// <c>NameTranslations</c> is a jsonb map of language to alternative. That keeps the unique index on
/// <c>(UserId, Name)</c> working, keeps <c>ORDER BY Name</c> meaning something, and means an
/// untranslated label falls back to the original rather than rendering blank.
/// </summary>
public static class LocalizedText
{
    public const string EmptyJson = "{}";

    public static JsonDocument Empty() => JsonDocument.Parse(EmptyJson);

    /// <summary>
    /// The best available string for <paramref name="language"/>: the translation if there is one,
    /// otherwise the canonical text. Never empty, and never the raw JSON.
    /// </summary>
    public static string Resolve(string canonical, JsonDocument? translations, string language)
    {
        if (translations is null || translations.RootElement.ValueKind != JsonValueKind.Object)
        {
            return canonical;
        }

        if (translations.RootElement.TryGetProperty(language, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } translated)
        {
            return translated;
        }

        return canonical;
    }

    /// <summary>The whole map, for an editing screen that needs every language at once.</summary>
    public static IReadOnlyDictionary<string, string> ToDictionary(JsonDocument? translations)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (translations is null || translations.RootElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in translations.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String
                && property.Value.GetString() is { Length: > 0 } value)
            {
                result[property.Name] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Turns a submitted map into something safe to store: unsupported languages dropped, keys
    /// normalised to their configured spelling, blanks removed, and every value held to the same
    /// length limit as the column it shadows.
    ///
    /// That last part matters. Without it, a 200-character limit on <c>Label</c> would be trivially
    /// bypassed by putting a megabyte of text in the translation instead.
    /// </summary>
    public static JsonDocument Sanitise(
        IReadOnlyDictionary<string, string>? translations,
        LocalizationOptions options,
        int maximumLength)
    {
        if (translations is null || translations.Count == 0)
        {
            return Empty();
        }

        var clean = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (language, value) in translations)
        {
            if (options.Normalise(language) is not { } normalisedLanguage)
            {
                // A language we cannot serve. Dropped rather than stored, so the map never fills up
                // with keys nothing will ever read.
                continue;
            }

            var trimmed = value?.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            clean[normalisedLanguage] = trimmed.Length > maximumLength
                ? trimmed[..maximumLength]
                : trimmed;
        }

        return clean.Count == 0
            ? Empty()
            : JsonDocument.Parse(JsonSerializer.Serialize(clean));
    }

    /// <summary>Which supported languages this map is still missing - what an admin screen would list.</summary>
    public static IReadOnlyList<string> MissingLanguages(JsonDocument? translations, LocalizationOptions options)
    {
        var present = ToDictionary(translations);

        return options.SupportedLanguages
            .Where(language => !string.Equals(language, options.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            .Where(language => !present.ContainsKey(language))
            .ToList();
    }
}
