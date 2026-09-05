namespace handytool_api.Models;

/// <summary>Configuration only; submitted record values are stored separately.</summary>
public class TimeField
{
    // Database columns
    public long FieldDefinitionId { get; set; }

    public TimeOnly? MinimumTime { get; set; }

    public TimeOnly? MaximumTime { get; set; }

    public int? StepSeconds { get; set; }

    // Relationships — not additional database columns
    public FieldDefinition FieldDefinition { get; set; } = null!;
}
