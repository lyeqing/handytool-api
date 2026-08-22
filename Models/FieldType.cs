namespace handytool_api.Models;

/// <summary>
/// The set of dynamic field types supported by the platform.
/// Persisted as text so the database stays readable; add new members at the end.
/// </summary>
public enum FieldType
{
    Text,
    LongText,
    Integer,
    Decimal,
    Boolean,
    Date,
    DateTime,
    Range,
    Dropdown,
    MultiSelect
}
