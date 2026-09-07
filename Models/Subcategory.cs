namespace handytool_api.Models;

public class Subcategory
{
    // Database columns
    public long Id { get; set; }

    public long MasterCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    // Relationships — not additional database columns
    public MasterCategory MasterCategory { get; set; } = null!;

    public ICollection<SubcategoryTranslation> Translations { get; set; } = new List<SubcategoryTranslation>();
}
