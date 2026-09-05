namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class ObjectField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public long ReferencedObjectDefinitionId { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;

    public ObjectDefinition ReferencedObjectDefinition { get; set; } = null!;
}
