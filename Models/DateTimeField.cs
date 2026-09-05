namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class DateTimeField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public DateTime? MinimumDateTime { get; set; }

    public DateTime? MaximumDateTime { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
