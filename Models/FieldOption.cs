namespace handytool_api.Models;

/// <summary>
/// One allowed choice for a <see cref="FieldType.Dropdown"/> or <see cref="FieldType.MultiSelect"/> field.
/// Options are relational, never embedded in <see cref="FieldDefinition.Settings"/>.
/// </summary>
public class FieldOption
{
    // Database columns
    public long Id { get; set; }

    public long FieldDefinitionId { get; set; }

    /// <summary>Stable value stored in record JSON, for example "water". Avoid changing once used.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Display text, for example "Water Damage". Safe to change without touching records.</summary>
    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    // Relationships — not additional database columns
    public ICollection<FieldOptionTranslation> Translations { get; set; } = new List<FieldOptionTranslation>();

    public FieldDefinition FieldDefinition { get; set; } = null!;
}
