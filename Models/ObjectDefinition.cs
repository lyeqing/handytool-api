using System.Text.Json;
using handytool_api.Localization;

namespace handytool_api.Models;

/// <summary>
/// Metadata describing one user-created logical object type, for example "Property Inspection".
/// This is schema, not data. Actual data always lives in <see cref="ObjectRecord"/>.
/// </summary>
public class ObjectDefinition
{
    public long Id { get; set; }

    /// <summary>Owning account. Resolved from the authenticated session, never from a request body.</summary>
    public long UserId { get; set; }

    public UserAccount User { get; set; } = null!;

    /// <summary>
    /// The canonical name, in the default language. Still the value the unique index and the sort
    /// order use, and the fallback whenever a translation is missing.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Language to translated name, for every language that is not the default. jsonb.</summary>
    public JsonDocument NameTranslations { get; set; } = LocalizedText.Empty();

    public string Description { get; set; } = string.Empty;

    public JsonDocument DescriptionTranslations { get; set; } = LocalizedText.Empty();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    public ICollection<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

    public ICollection<ObjectRecord> Records { get; set; } = new List<ObjectRecord>();
}
