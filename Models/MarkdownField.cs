namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class MarkdownField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public int? MinimumLength { get; set; }

    public int? MaximumLength { get; set; }

    public string? Placeholder { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
