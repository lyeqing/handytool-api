namespace handytool_api.Models;

public class FieldOptionTranslation
{
    // Database columns
    public long FieldOptionId { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    // Relationships — not additional database columns
    public FieldOption FieldOption { get; set; } = null!;
}
