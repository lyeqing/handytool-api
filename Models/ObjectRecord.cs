using System.Text.Json;

namespace handytool_api.Models;

/// <summary>
/// The one and only entity representing user-entered business data.
/// Every user-created object type ("Property Inspection", "Customer Survey", ...) stores its
/// rows here; the dynamic part lives in <see cref="Values"/> as jsonb.
/// </summary>
public class ObjectRecord
{
    // Database columns
    public long Id { get; set; }

    public long ObjectDefinitionId { get; set; }

    /// <summary>Original creator. Preserved when company membership changes.</summary>
    public long CreatedByUserId { get; set; }

    /// <summary>Owning company at creation; null for independent users' personal records.</summary>
    public long? CompanyId { get; set; }

    /// <summary>Application-managed concurrency token; increment on each successful update.</summary>
    public long Revision { get; set; } = 1;

    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Dynamic field values keyed by <see cref="FieldDefinition.Key"/>.
    /// Always a JSON object (enforced by a database check constraint); defaults to <c>{}</c>.
    /// Contains values only - never labels, types or validation metadata.
    /// </summary>
    public JsonDocument Values { get; set; } = RecordValues.Empty();

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    // Relationships — not additional database columns
    public UserAccount CreatedByUser { get; set; } = null!;

    public CompanyAccount? Company { get; set; }

    public ObjectDefinition ObjectDefinition { get; set; } = null!;
}

/// <summary>Helpers for the default record values document.</summary>
public static class RecordValues
{
    public const string EmptyJson = "{}";

    public static JsonDocument Empty() => JsonDocument.Parse(EmptyJson);
}
