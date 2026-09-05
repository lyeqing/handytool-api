namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class RangeField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public decimal? Minimum { get; set; }

    public decimal? Maximum { get; set; }

    public decimal? Step { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
