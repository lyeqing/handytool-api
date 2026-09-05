namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class MultiSelectField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public int? MinimumSelections { get; set; }

    public int? MaximumSelections { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
