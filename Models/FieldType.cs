namespace handytool_api.Models;

/// <summary>Supported field components. Persisted integer values must never be renumbered or reused.</summary>
public enum FieldType
{
    ShortText = 1,
    LongText = 2,
    Integer = 3,
    Decimal = 4,
    Range = 5,
    Date = 6,
    Time = 7,
    DateTime = 8,
    Dropdown = 9,
    RadioGroup = 10,
    Checklist = 11,
    MultiSelect = 12,
    Boolean = 13,
    Markdown = 14,
    Object = 15,
    Collection = 16,
}
