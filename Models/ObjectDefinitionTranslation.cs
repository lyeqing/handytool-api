namespace handytool_api.Models;

public class ObjectDefinitionTranslation
{
    // Database columns
    public long ObjectDefinitionId { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>Null means use the parent entity's original name.</summary>
    public string? Name { get; set; }

    /// <summary>Null means use the parent entity's original description.</summary>
    public string? Description { get; set; }

    // Relationships — not additional database columns
    public ObjectDefinition ObjectDefinition { get; set; } = null!;
}
