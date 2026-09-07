using System.Text.Json;
using handytool_api.Models;
using handytool_api.Validation;
using static handytool_api.Tests.PropertyInspection;

namespace handytool_api.Tests;

public class RecordValueValidatorTests
{
    private static RecordValidationResult Validate(string json, List<FieldDefinition>? fields = null) =>
        RecordValueValidator.Validate(fields ?? Fields(), Json(json));

    private static void AssertSingleError(RecordValidationResult result, string fieldKey, string errorCode)
    {
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(fieldKey, error.FieldKey);
        Assert.Equal(errorCode, error.ErrorCode);
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public void Valid_record_passes()
    {
        var result = RecordValueValidator.Validate(Fields(), ValidValues());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Optional_field_may_be_missing_or_explicitly_null()
    {
        var missing = Validate("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8,
              "damageFound": false
            }
            """);
        Assert.True(missing.IsValid);

        var explicitNull = Validate("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8,
              "damageFound": false,
              "damageType": null
            }
            """);
        Assert.True(explicitNull.IsValid);
    }

    [Fact]
    public void Values_must_be_a_json_object()
    {
        var result = RecordValueValidator.Validate(Fields(), Json("""[1, 2, 3]"""));

        AssertSingleError(result, string.Empty, RecordValidationErrorCodes.ValuesNotObject);
    }

    [Fact]
    public void Unknown_field_keys_are_rejected()
    {
        var result = Validate("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8,
              "damageFound": false,
              "inspectorMood": "cheerful"
            }
            """);

        AssertSingleError(result, "inspectorMood", RecordValidationErrorCodes.UnknownField);
    }

    [Fact]
    public void Missing_required_fields_are_reported_per_field()
    {
        var result = Validate("{}");

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Errors.Count);
        Assert.All(result.Errors, e => Assert.Equal(RecordValidationErrorCodes.Required, e.ErrorCode));
        Assert.Equal(
            ["propertyAddress", "inspectionDate", "conditionScore", "damageFound"],
            result.Errors.Select(e => e.FieldKey));
    }

    [Fact]
    public void Required_text_field_rejects_whitespace()
    {
        var result = Validate("""
            {
              "propertyAddress": "   ",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8,
              "damageFound": false
            }
            """);

        AssertSingleError(result, "propertyAddress", RecordValidationErrorCodes.Required);
    }

    [Fact]
    public void Numeric_strings_are_not_coerced()
    {
        var result = Validate("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": "8",
              "damageFound": false
            }
            """);

        AssertSingleError(result, "conditionScore", RecordValidationErrorCodes.InvalidType);
    }

    [Fact]
    public void Boolean_strings_are_not_coerced()
    {
        var result = Validate("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8,
              "damageFound": "true"
            }
            """);

        AssertSingleError(result, "damageFound", RecordValidationErrorCodes.InvalidType);
    }

    [Theory]
    [InlineData(0, RecordValidationErrorCodes.BelowMinimum)]
    [InlineData(11, RecordValidationErrorCodes.AboveMaximum)]
    public void Range_bounds_are_enforced(int value, string expectedCode)
    {
        var result = Validate($$"""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": {{value}},
              "damageFound": false
            }
            """);

        AssertSingleError(result, "conditionScore", expectedCode);
    }

    [Fact]
    public void Range_step_is_enforced()
    {
        var result = Validate("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8.5,
              "damageFound": false
            }
            """);

        AssertSingleError(result, "conditionScore", RecordValidationErrorCodes.InvalidStep);
    }

    [Fact]
    public void Dropdown_value_must_be_a_known_active_option()
    {
        var unknown = Validate("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8,
              "damageFound": true,
              "damageType": "fire"
            }
            """);
        AssertSingleError(unknown, "damageType", RecordValidationErrorCodes.UnknownOption);

        var fields = Fields();
        var damageType = fields.Single(f => f.Key == "damageType");
        damageType.Options = [Option("water", "Water Damage", isActive: false)];

        var inactive = RecordValueValidator.Validate(fields, ValidValues());
        AssertSingleError(inactive, "damageType", RecordValidationErrorCodes.UnknownOption);
    }

    [Fact]
    public void Dropdown_labels_are_not_accepted_in_place_of_values()
    {
        var result = Validate("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8,
              "damageFound": true,
              "damageType": "Water Damage"
            }
            """);

        AssertSingleError(result, "damageType", RecordValidationErrorCodes.UnknownOption);
    }

    [Fact]
    public void Inactive_fields_are_not_validated_and_their_keys_are_unknown()
    {
        var fields = Fields();
        fields.Single(f => f.Key == "damageFound").IsActive = false;

        var withoutKey = RecordValueValidator.Validate(fields, Json("""
            {
              "propertyAddress": "10 King William Street",
              "inspectionDate": "2026-08-22",
              "conditionScore": 8
            }
            """));
        Assert.True(withoutKey.IsValid);

        var withKey = RecordValueValidator.Validate(fields, ValidValues());
        Assert.Contains(withKey.Errors, e =>
            e.FieldKey == "damageFound" && e.ErrorCode == RecordValidationErrorCodes.UnknownField);
    }

    [Theory]
    [InlineData("\"2026-13-01\"")]
    [InlineData("\"22/08/2026\"")]
    [InlineData("\"2026-08-22T04:30:00Z\"")]
    public void Date_fields_require_iso_dates(string json)
    {
        var fields = new List<FieldDefinition> { Field("inspectionDate", "Inspection Date", FieldType.Date) };

        var result = RecordValueValidator.Validate(fields, Json($$"""{ "inspectionDate": {{json}} }"""));

        AssertSingleError(result, "inspectionDate", RecordValidationErrorCodes.InvalidFormat);
    }

    [Fact]
    public void Date_bounds_are_enforced()
    {
        var fields = new List<FieldDefinition>
        {
            Field("inspectionDate", "Inspection Date", FieldType.Date,
                settings: """{ "minimumDate": "2026-01-01", "maximumDate": "2026-12-31" }""")
        };

        var result = RecordValueValidator.Validate(fields, Json("""{ "inspectionDate": "2025-12-31" }"""));

        AssertSingleError(result, "inspectionDate", RecordValidationErrorCodes.BelowMinimum);
    }

    [Theory]
    [InlineData("\"2026-08-22T04:30:00Z\"", true)]
    [InlineData("\"2026-08-22T04:30:00+09:30\"", true)]
    [InlineData("\"2026-08-22T04:30\"", true)]
    [InlineData("\"2026-08-22\"", false)]
    [InlineData("\"22 August 2026 4:30pm\"", false)]
    public void DateTime_fields_require_iso_8601(string json, bool expectedValid)
    {
        var fields = new List<FieldDefinition> { Field("startedAt", "Started At", FieldType.DateTime) };

        var result = RecordValueValidator.Validate(fields, Json($$"""{ "startedAt": {{json}} }"""));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Integer_fields_reject_fractional_numbers()
    {
        var fields = new List<FieldDefinition> { Field("roomCount", "Room Count", FieldType.Integer) };

        var result = RecordValueValidator.Validate(fields, Json("""{ "roomCount": 3.5 }"""));

        AssertSingleError(result, "roomCount", RecordValidationErrorCodes.InvalidType);
    }

    [Fact]
    public void Decimal_fields_accept_whole_and_fractional_numbers()
    {
        var fields = new List<FieldDefinition>
        {
            Field("price", "Price", FieldType.Decimal, settings: """{ "minimum": 0, "maximum": 1000 }""")
        };

        Assert.True(RecordValueValidator.Validate(fields, Json("""{ "price": 12 }""")).IsValid);
        Assert.True(RecordValueValidator.Validate(fields, Json("""{ "price": 12.75 }""")).IsValid);

        var tooLarge = RecordValueValidator.Validate(fields, Json("""{ "price": 1000.01 }"""));
        AssertSingleError(tooLarge, "price", RecordValidationErrorCodes.AboveMaximum);
    }

    [Fact]
    public void Text_length_settings_are_enforced()
    {
        var fields = new List<FieldDefinition>
        {
            Field("notes", "Notes", FieldType.ShortText, settings: """{ "minimumLength": 5, "maximumLength": 10 }""")
        };

        AssertSingleError(
            RecordValueValidator.Validate(fields, Json("""{ "notes": "hi" }""")),
            "notes",
            RecordValidationErrorCodes.TooShort);

        AssertSingleError(
            RecordValueValidator.Validate(fields, Json("""{ "notes": "far too long to fit" }""")),
            "notes",
            RecordValidationErrorCodes.TooLong);
    }

    [Fact]
    public void MultiSelect_requires_an_array_of_known_option_values()
    {
        var fields = MultiSelectField(isRequired: false);

        Assert.True(RecordValueValidator.Validate(
            fields,
            Json("""{ "contactMethods": ["email", "sms"] }""")).IsValid);

        AssertSingleError(
            RecordValueValidator.Validate(fields, Json("""{ "contactMethods": "email" }""")),
            "contactMethods",
            RecordValidationErrorCodes.InvalidType);

        AssertSingleError(
            RecordValueValidator.Validate(fields, Json("""{ "contactMethods": ["email", "pigeon"] }""")),
            "contactMethods",
            RecordValidationErrorCodes.UnknownOption);

        AssertSingleError(
            RecordValueValidator.Validate(fields, Json("""{ "contactMethods": ["email", "email"] }""")),
            "contactMethods",
            RecordValidationErrorCodes.DuplicateOption);

        AssertSingleError(
            RecordValueValidator.Validate(fields, Json("""{ "contactMethods": [1] }""")),
            "contactMethods",
            RecordValidationErrorCodes.InvalidType);
    }

    [Fact]
    public void Required_multiselect_rejects_an_empty_array()
    {
        var result = RecordValueValidator.Validate(
            MultiSelectField(isRequired: true),
            Json("""{ "contactMethods": [] }"""));

        AssertSingleError(result, "contactMethods", RecordValidationErrorCodes.Required);
    }

    private static List<FieldDefinition> MultiSelectField(bool isRequired) =>
    [
        Field("contactMethods", "Contact Methods", FieldType.MultiSelect, isRequired,
            options: [Option("email", "Email"), Option("sms", "SMS"), Option("phone", "Phone")])
    ];
}
