namespace handytool_api.Models;

/// <summary>
/// Metadata for an object property or a standalone, reusable collection item definition.
/// </summary>
public class FieldDefinition
{
    // Database columns
    public long Id { get; set; }

    public long? ObjectDefinitionId { get; set; }

    /// <summary>
    /// Stable machine-readable identifier used as the property name inside
    /// <see cref="ObjectRecord.Values"/>, for example "propertyAddress".
    /// Must not change once records exist without a data migration.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>User-facing label, for example "Property Address". Safe to change at any time.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public FieldType FieldType { get; set; }

    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    // Relationships — not additional database columns
    public ICollection<FieldDefinitionTranslation> Translations { get; set; } = new List<FieldDefinitionTranslation>();

    // Exactly one matching configuration is required at transaction completion.
    public ShortTextField? ShortTextField { get; set; }

    public LongTextField? LongTextField { get; set; }

    public IntegerField? IntegerField { get; set; }

    public DecimalField? DecimalField { get; set; }

    public RangeField? RangeField { get; set; }

    public DateField? DateField { get; set; }

    public TimeField? TimeField { get; set; }

    public DateTimeField? DateTimeField { get; set; }

    public DropdownField? DropdownField { get; set; }

    public RadioGroupField? RadioGroupField { get; set; }

    public ChecklistField? ChecklistField { get; set; }

    public MultiSelectField? MultiSelectField { get; set; }

    public BooleanField? BooleanField { get; set; }

    public MarkdownField? MarkdownField { get; set; }

    public ObjectField? ObjectField { get; set; }

    public CollectionField? CollectionField { get; set; }

    public ObjectDefinition? ObjectDefinition { get; set; }

    public ICollection<FieldOption> Options { get; set; } = new List<FieldOption>();
}
