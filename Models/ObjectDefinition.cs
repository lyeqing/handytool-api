namespace handytool_api.Models;

/// <summary>
/// Metadata describing one user-created logical object type, for example "Property Inspection".
/// This is schema, not data. Actual data always lives in <see cref="ObjectRecord"/>.
/// </summary>
public class ObjectDefinition
{
    // Database columns
    public long Id { get; set; }

    public long MasterCategoryId { get; set; } = MasterCategory.UncategorizedId;

    public long? SubcategoryId { get; set; }

    /// <summary>Creator for audit and private visibility; this does not make shared definitions user-owned.</summary>
    public long CreatedByUserId { get; set; }

    /// <summary>
    /// The canonical name, in the default language. Still the value the unique index and the sort
    /// order use, and the fallback whenever a translation is missing.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Minimum subscription access, 0 (anonymous) through 3 (Full); visibility also applies.</summary>
    public int RequiredAccessLevel { get; set; }

    public DefinitionVisibility Visibility { get; set; } = DefinitionVisibility.Private;

    /// <summary>The explicitly selected company receiving access, only for Company visibility.</summary>
    public long? CompanyId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    // Relationships — not additional database columns
    public MasterCategory MasterCategory { get; set; } = null!;

    public Subcategory? Subcategory { get; set; }

    public UserAccount CreatedByUser { get; set; } = null!;

    public ICollection<ObjectDefinitionTranslation> Translations { get; set; } = new List<ObjectDefinitionTranslation>();

    public CompanyAccount? Company { get; set; }

    public ICollection<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

    public ICollection<ObjectRecord> Records { get; set; } = new List<ObjectRecord>();
}
