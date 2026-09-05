namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class CollectionField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public int? MinimumItems { get; set; }

    public int? MaximumItems { get; set; }

    /// <summary>One standalone definition supplies the type and settings for every item.</summary>
    public long ItemDefinitionId { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;

    public FieldDefinition ItemDefinition { get; set; } = null!;
}
