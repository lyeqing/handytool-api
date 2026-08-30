using System.Text.Json;
using handytool_api.Localization;

namespace handytool_api.Models;

/// <summary>
/// One allowed choice for a <see cref="FieldType.Dropdown"/> or <see cref="FieldType.MultiSelect"/> field.
/// Options are relational, never embedded in <see cref="FieldDefinition.Settings"/>.
/// </summary>
public class FieldOption
{
    public long Id { get; set; }

    public long FieldDefinitionId { get; set; }

    /// <summary>Stable value stored in record JSON, for example "water". Avoid changing once used.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Display text, for example "Water Damage". Safe to change without touching records.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Language to translated label. This is where the separation of <see cref="Value"/> from
    /// <see cref="Label"/> pays off: "water" stays "water" in every record ever written, while the
    /// text a person reads can be translated freely.
    /// </summary>
    public JsonDocument LabelTranslations { get; set; } = LocalizedText.Empty();

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    public FieldDefinition FieldDefinition { get; set; } = null!;
}
