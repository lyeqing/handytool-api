namespace handytool_api.Models;

public class MasterCategoryTranslation
{
    // Database columns
    public long MasterCategoryId { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>Null means use the parent entity's original name.</summary>
    public string? Name { get; set; }

    /// <summary>Null means use the parent entity's original description.</summary>
    public string? Description { get; set; }

    // Relationships — not additional database columns
    public MasterCategory MasterCategory { get; set; } = null!;
}
