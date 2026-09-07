namespace handytool_api.Models;

public class MasterCategory
{
    // Constants
    // Negative seed ID leaves the positive identity sequence available for admin-created categories.
    public const long UncategorizedId = -1;

    // Database columns
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    // Relationships — not additional database columns
    public ICollection<MasterCategoryTranslation> Translations { get; set; } = new List<MasterCategoryTranslation>();

    public ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();
}
