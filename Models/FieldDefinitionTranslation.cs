namespace handytool_api.Models;

public class FieldDefinitionTranslation
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>Null means use the parent entity's original name.</summary>
    public string? Name { get; set; }

    /// <summary>Null means use the parent entity's original description.</summary>
    public string? Description { get; set; }

    public string? Placeholder { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
