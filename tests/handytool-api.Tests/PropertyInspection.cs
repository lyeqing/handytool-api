using System.Text.Json;
using handytool_api.Models;

namespace handytool_api.Tests;

/// <summary>
/// The "Property Inspection" definition from the platform design, expressed as plain field
/// definitions so the validator can be tested without a database.
/// </summary>
internal static class PropertyInspection
{
    public static List<FieldDefinition> Fields() =>
    [
        Field("propertyAddress", "Property Address", FieldType.Text, isRequired: true,
            settings: """{ "minimumLength": 2, "maximumLength": 200 }"""),
        Field("inspectionDate", "Inspection Date", FieldType.Date, isRequired: true),
        Field("conditionScore", "Condition Score", FieldType.Range, isRequired: true,
            settings: """{ "minimum": 1, "maximum": 10, "step": 1 }"""),
        Field("damageFound", "Damage Found", FieldType.Boolean, isRequired: true),
        Field("damageType", "Damage Type", FieldType.Dropdown,
            options:
            [
                Option("water", "Water Damage"),
                Option("structural", "Structural Damage"),
                Option("cosmetic", "Cosmetic Damage")
            ])
    ];

    public static JsonElement ValidValues() => Json("""
        {
          "propertyAddress": "10 King William Street",
          "inspectionDate": "2026-08-22",
          "conditionScore": 8,
          "damageFound": true,
          "damageType": "water"
        }
        """);

    public static FieldDefinition Field(
        string key,
        string name,
        FieldType fieldType,
        bool isRequired = false,
        bool isActive = true,
        string settings = "{}",
        List<FieldOption>? options = null) => new()
    {
        ObjectDefinitionId = 12,
        Key = key,
        Name = name,
        FieldType = fieldType,
        IsRequired = isRequired,
        IsActive = isActive,
        Settings = JsonDocument.Parse(settings),
        Options = options ?? []
    };

    public static FieldOption Option(string value, string label, bool isActive = true) => new()
    {
        Value = value,
        Label = label,
        IsActive = isActive
    };

    /// <summary>Parses a JSON literal into a long-lived <see cref="JsonElement"/>.</summary>
    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
