using handytool_api.Models;
using handytool_api.Services;
using handytool_api.Validation;
using static handytool_api.Tests.PropertyInspection;

namespace handytool_api.Tests;

public class FieldConfigurationTests
{
    [Fact]
    public void Date_time_bounds_are_normalized_and_enforced()
    {
        var field = Field("appointment", "Appointment", FieldType.DateTime, settings: """
            { "minimumDateTime": "2026-09-07T09:30:00+09:30",
              "maximumDateTime": "2026-09-08T09:30:00+09:30" }
            """);

        Assert.Equal(new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc), field.DateTimeField!.MinimumDateTime);
        Assert.Equal(DateTimeKind.Utc, field.DateTimeField.MinimumDateTime!.Value.Kind);
        Assert.True(RecordValueValidator.Validate([field], Json("""{"appointment":"2026-09-07T12:00:00Z"}""")).IsValid);
        var result = RecordValueValidator.Validate([field], Json("""{"appointment":"2026-09-06T23:59:00Z"}"""));
        Assert.Equal(RecordValidationErrorCodes.BelowMinimum, Assert.Single(result.Errors).ErrorCode);
    }

    [Theory]
    [InlineData("""{"minimumDateTime":"2026-09-07T00:00:00Z"}""")]
    [InlineData("""{"maximumDateTime":"2026-09-07T00:00:00Z"}""")]
    public void Either_date_time_bound_can_be_used_alone(string settings)
    {
        var field = Field("appointment", "Appointment", FieldType.DateTime, settings: settings);
        Assert.NotNull(field.DateTimeField);
    }

    [Theory]
    [InlineData(FieldType.DateTime, """{"minimumDateTime":"2026-09-08T00:00:00Z","maximumDateTime":"2026-09-07T00:00:00Z"}""")]
    [InlineData(FieldType.DateTime, """{"minimumDateTime":"2026-09-07T00:00:00"}""")]
    [InlineData(FieldType.Integer, """{"step":0}""")]
    [InlineData(FieldType.Decimal, """{"step":-1}""")]
    [InlineData(FieldType.ShortText, """{"minimumLength":-1}""")]
    [InlineData(FieldType.Collection, """{"minimumItems":-1}""")]
    public void Invalid_bounds_and_numeric_settings_still_return_validation_errors(FieldType type, string settings)
    {
        var failure = Assert.Throws<ApiFailure>(() => Field("field", "Field", type, settings: settings));
        Assert.Equal(400, failure.Status);
        Assert.Equal("invalid_settings", failure.Code);
    }
}
