namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class IntegerField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public long? Minimum { get; set; }

    public long? Maximum { get; set; }

    public long? Step { get; set; }

    public string? Placeholder { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
