namespace handytool_api.Configuration;

/// <summary>
/// Bound from the "Localization" section.
///
/// The supported list is configuration rather than an enum so that adding a third language is a
/// settings change plus translation data, not a migration and a redeploy. It is also the allow-list:
/// a language tag that is not in here never reaches the database, from a request body or anywhere
/// else.
/// </summary>
public sealed class LocalizationOptions
{
    public const string SectionName = "Localization";

    /// <summary>
    /// BCP-47 tags. "zh-Hans" rather than "zh" because Simplified and Traditional are different
    /// writing systems, and a reader of one does not necessarily read the other.
    /// </summary>
    public string[] SupportedLanguages { get; set; } = ["en", "zh-Hans"];

    /// <summary>Used when nothing else resolves, and the language canonical text is assumed to be in.</summary>
    public string DefaultLanguage { get; set; } = "en";

    public bool IsSupported(string? language) =>
        language is not null
        && SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the configured spelling of a supported tag, so "ZH-HANS" and "zh-hans" both become
    /// "zh-Hans" and cannot become two different keys in a translations map.
    /// </summary>
    public string? Normalise(string? language) =>
        language is null
            ? null
            : SupportedLanguages.FirstOrDefault(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase));
}
