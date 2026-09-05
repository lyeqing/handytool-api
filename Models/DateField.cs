namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class DateField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public DateOnly? MinimumDate { get; set; }

    public DateOnly? MaximumDate { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
