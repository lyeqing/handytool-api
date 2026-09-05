namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class DropdownField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public string? Placeholder { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
