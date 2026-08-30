using System.Text.Json;
using handytool_api.Localization;

namespace handytool_api.Models;

/// <summary>
/// Metadata describing one dynamic field belonging to an <see cref="ObjectDefinition"/>.
/// </summary>
public class FieldDefinition
{
    public long Id { get; set; }

    public long ObjectDefinitionId { get; set; }

    /// <summary>
    /// Stable machine-readable identifier used as the property name inside
    /// <see cref="ObjectRecord.Values"/>, for example "propertyAddress".
    /// Must not change once records exist without a data migration.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>User-facing label, for example "Property Address". Safe to change at any time.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Language to translated label. The <see cref="Key"/> is never translated.</summary>
    public JsonDocument NameTranslations { get; set; } = LocalizedText.Empty();

    public string? Description { get; set; }

    public JsonDocument DescriptionTranslations { get; set; } = LocalizedText.Empty();

    public FieldType FieldType { get; set; }

    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    /// <summary>
    /// Type-specific settings stored as jsonb, for example minimum/maximum/step.
    /// Always a JSON object; defaults to <c>{}</c>. Never contains dropdown options.
    /// </summary>
    public JsonDocument Settings { get; set; } = FieldSettings.Empty();

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    public ObjectDefinition ObjectDefinition { get; set; } = null!;

    public ICollection<FieldOption> Options { get; set; } = new List<FieldOption>();
}

/// <summary>Helpers for the default field settings document.</summary>
public static class FieldSettings
{
    public const string EmptyJson = "{}";

    public static JsonDocument Empty() => JsonDocument.Parse(EmptyJson);
}
