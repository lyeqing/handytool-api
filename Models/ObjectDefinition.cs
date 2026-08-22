namespace handytool_api.Models;

/// <summary>
/// Metadata describing one user-created logical object type, for example "Property Inspection".
/// This is schema, not data. Actual data always lives in <see cref="ObjectRecord"/>.
/// </summary>
public class ObjectDefinition
{
    public long Id { get; set; }

    /// <summary>Owning user/account/tenant. Never accepted from a client request body.</summary>
    public long OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    public ICollection<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

    public ICollection<ObjectRecord> Records { get; set; } = new List<ObjectRecord>();
}
