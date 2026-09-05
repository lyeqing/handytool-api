using System.Globalization;
using System.Text.Json;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Services;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Validation;

/// <summary>
/// Validates <see cref="ObjectRecord.Values"/> against the active field definitions of an
/// <see cref="ObjectDefinition"/>. Nothing may persist dynamic values without passing through here.
/// Types are never coerced: "8" is not accepted for a numeric field.
/// </summary>
public sealed class RecordValueValidator
{
    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
    ];

    private readonly HandyToolDbContext _db;

    public RecordValueValidator(HandyToolDbContext db) => _db = db;

    /// <summary>Loads the active fields and active options of a definition, then validates.</summary>
    public async Task<RecordValidationResult> ValidateAsync(
        long objectDefinitionId,
        JsonElement values,
        CancellationToken cancellationToken = default)
    {
        var definition = await new DefinitionLoader(_db, new AccessService(_db)).LoadAsync(
            objectDefinitionId, new AccessActor(null,null,null,true,3,1), cancellationToken);
        return Validate(definition?.Fields.ToList() ?? [], values);
    }

    /// <summary>
    /// Pure validation against a supplied set of field definitions.
    /// Inactive fields and inactive options are ignored even if they are passed in.
    /// </summary>
    public static RecordValidationResult Validate(IReadOnlyCollection<FieldDefinition> fields, JsonElement values) => ValidateFields(fields, values, 0);

    private static RecordValidationResult ValidateFields(IReadOnlyCollection<FieldDefinition> fields, JsonElement values, int depth)
    {
        var errors = new List<RecordValidationError>();
        if (depth > 8) return new RecordValidationResult([new("", "nesting_limit", "Values are nested too deeply.")]);

        if (values.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RecordValidationError(
                string.Empty,
                RecordValidationErrorCodes.ValuesNotObject,
                "Values must be a JSON object."));
            return new RecordValidationResult(errors);
        }

        var activeFields = fields.Where(f => f.IsActive).ToList();
        var knownKeys = activeFields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in values.EnumerateObject())
        {
            if (!seenKeys.Add(property.Name)) errors.Add(new(property.Name, "duplicate_key", "Duplicate field key."));
            if (!knownKeys.Contains(property.Name))
            {
                errors.Add(new RecordValidationError(
                    property.Name,
                    RecordValidationErrorCodes.UnknownField,
                    $"'{property.Name}' is not a field of this object definition."));
            }
        }

        foreach (var field in activeFields)
        {
            var present = values.TryGetProperty(field.Key, out var value)
                          && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

            if (!present)
            {
                if (field.IsRequired)
                {
                    errors.Add(Required(field));
                }

                continue;
            }

            ValidateValue(field, value, errors, depth);
        }

        return errors.Count == 0 ? RecordValidationResult.Success : new RecordValidationResult(errors);
    }

    private static void ValidateValue(FieldDefinition field, JsonElement value, List<RecordValidationError> errors, int depth)
    {
        switch (field.FieldType)
        {
            case FieldType.ShortText:
            case FieldType.Markdown:
            case FieldType.LongText:
                ValidateText(field, value, errors);
                break;
            case FieldType.Integer:
                ValidateInteger(field, value, errors);
                break;
            case FieldType.Decimal:
                ValidateDecimal(field, value, errors);
                break;
            case FieldType.Boolean:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    errors.Add(InvalidType(field, "a JSON boolean", value));
                }

                break;
            case FieldType.Date:
                ValidateDate(field, value, errors);
                break;
            case FieldType.DateTime:
                ValidateDateTime(field, value, errors);
                break;
            case FieldType.Range:
                ValidateRange(field, value, errors);
                break;
            case FieldType.RadioGroup:
            case FieldType.Dropdown:
                ValidateDropdown(field, value, errors);
                break;
            case FieldType.Checklist:
            case FieldType.MultiSelect:
                ValidateMultiSelect(field, value, errors);
                break;
            case FieldType.Object:
                if (field.ObjectField?.ReferencedObjectDefinition is not { IsActive: true } objectDefinition)
                    errors.Add(new(field.Key, "definition_inactive", "Nested object definition is unavailable."));
                else
                    errors.AddRange(ValidateFields(objectDefinition.Fields.ToList(), value, depth + 1).Errors
                        .Select(e => e with { FieldKey = field.Key + (e.FieldKey.Length > 0 ? "." + e.FieldKey : "") }));
                break;
            case FieldType.Collection:
                if (value.ValueKind != JsonValueKind.Array) { errors.Add(InvalidType(field, "an array", value)); break; }
                var length = value.GetArrayLength();
                if (length > 1000) { errors.Add(new(field.Key,"too_many_items","Collections support at most 1000 items.")); break; }
                if (depth >= 8) { errors.Add(new(field.Key,"nesting_limit","Values are nested too deeply.")); break; }
                var collection = field.CollectionField!;
                if (field.IsRequired && length == 0) errors.Add(Required(field));
                if (collection.MinimumItems is { } minItems && length < minItems) errors.Add(new(field.Key,"too_few_items","Too few items."));
                if (collection.MaximumItems is { } maxItems && length > maxItems) errors.Add(new(field.Key,"too_many_items","Too many items."));
                for (var i = 0; i < length; i++)
                {
                    var itemErrors = new List<RecordValidationError>();
                    if (value[i].ValueKind == JsonValueKind.Null) itemErrors.Add(new("", "invalid_type", "Collection items cannot be null."));
                    else ValidateValue(collection.ItemDefinition, value[i], itemErrors, depth + 1);
                    errors.AddRange(itemErrors.Select(e => e with { FieldKey = $"{field.Key}[{i}]" +
                        (e.FieldKey.StartsWith(collection.ItemDefinition.Key + ".") ? e.FieldKey[collection.ItemDefinition.Key.Length..] : "") }));
                }
                break;
            case FieldType.Time:
                if (value.ValueKind != JsonValueKind.String || !TimeOnly.TryParseExact(value.GetString(),
                    ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                { errors.Add(new(field.Key,"invalid_format","Time must use HH:mm or HH:mm:ss.")); break; }
                var bounds = field.TimeField!;
                if (bounds.MinimumTime is { } minTime && time < minTime) errors.Add(new(field.Key,"below_minimum","Time is too early."));
                if (bounds.MaximumTime is { } maxTime && time > maxTime) errors.Add(new(field.Key,"above_maximum","Time is too late."));
                if (bounds.StepSeconds is { } seconds && (time.ToTimeSpan().TotalSeconds - (bounds.MinimumTime ?? TimeOnly.MinValue).ToTimeSpan().TotalSeconds) % seconds != 0)
                    errors.Add(new(field.Key,"invalid_step","Time does not match the configured step."));
                break;
            default:
                // A field type was added to the enum without validation support - fail loudly rather
                // than silently accepting unvalidated data.
                errors.Add(new RecordValidationError(
                    field.Key,
                    RecordValidationErrorCodes.InvalidType,
                    $"Field type '{field.FieldType}' is not supported yet."));
                break;
        }
    }

    private static void ValidateText(FieldDefinition field, JsonElement value, List<RecordValidationError> errors)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(InvalidType(field, "a JSON string", value));
            return;
        }

        var text = value.GetString() ?? string.Empty;

        if (field.IsRequired && string.IsNullOrWhiteSpace(text))
        {
            errors.Add(Required(field));
            return;
        }

        var minimumLength = GetInt32Setting(field, "minimumLength");
        if (minimumLength is { } min && text.Length < min)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.TooShort,
                $"'{field.Name}' must be at least {min} characters."));
        }

        var maximumLength = GetInt32Setting(field, "maximumLength");
        if (maximumLength is { } max && text.Length > max)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.TooLong,
                $"'{field.Name}' must be at most {max} characters."));
        }
    }

    private static void ValidateInteger(FieldDefinition field, JsonElement value, List<RecordValidationError> errors)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            errors.Add(InvalidType(field, "a JSON number", value));
            return;
        }

        if (!value.TryGetInt64(out var number))
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.InvalidType,
                $"'{field.Name}' must be a whole number without a fractional part."));
            return;
        }

        ValidateNumericBounds(field, number, errors);
    }

    private static void ValidateDecimal(FieldDefinition field, JsonElement value, List<RecordValidationError> errors)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            errors.Add(InvalidType(field, "a JSON number", value));
            return;
        }

        if (!value.TryGetDecimal(out var number))
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.InvalidType,
                $"'{field.Name}' must be a decimal number within the supported range."));
            return;
        }

        ValidateNumericBounds(field, number, errors);
    }

    private static void ValidateRange(FieldDefinition field, JsonElement value, List<RecordValidationError> errors) =>
        ValidateDecimal(field, value, errors);
    private static void ValidateNumericBounds(FieldDefinition field, decimal number, List<RecordValidationError> errors)
    {
        var step = GetDecimalSetting(field, "step");
        if (step is > 0)
        {
            var origin = GetDecimalSetting(field, "minimum") ?? 0m;
            // Taking each remainder first avoids overflow when values span the decimal range.
            if (((number % step.Value) - (origin % step.Value)) % step.Value != 0m)
                errors.Add(new(field.Key, RecordValidationErrorCodes.InvalidStep, "Value does not match the configured step."));
        }
        var minimum = GetDecimalSetting(field, "minimum");
        if (minimum is { } min && number < min)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.BelowMinimum,
                $"'{field.Name}' must be greater than or equal to {min}."));
        }

        var maximum = GetDecimalSetting(field, "maximum");
        if (maximum is { } max && number > max)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.AboveMaximum,
                $"'{field.Name}' must be less than or equal to {max}."));
        }
    }

    private static void ValidateDate(FieldDefinition field, JsonElement value, List<RecordValidationError> errors)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(InvalidType(field, "an ISO date string (YYYY-MM-DD)", value));
            return;
        }

        var text = value.GetString() ?? string.Empty;
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.InvalidFormat,
                $"'{field.Name}' must be a date formatted as YYYY-MM-DD."));
            return;
        }

        var minimumDate = GetDateSetting(field, "minimumDate");
        if (minimumDate is { } min && date < min)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.BelowMinimum,
                $"'{field.Name}' must be on or after {min:yyyy-MM-dd}."));
        }

        var maximumDate = GetDateSetting(field, "maximumDate");
        if (maximumDate is { } max && date > max)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.AboveMaximum,
                $"'{field.Name}' must be on or before {max:yyyy-MM-dd}."));
        }
    }

    private static void ValidateDateTime(FieldDefinition field, JsonElement value, List<RecordValidationError> errors)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(InvalidType(field, "an ISO 8601 datetime string", value));
            return;
        }

        var text = value.GetString() ?? string.Empty;
        if (!TryParseDateTime(text, out var moment))
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.InvalidFormat,
                $"'{field.Name}' must be an ISO 8601 datetime, for example 2026-08-22T04:30:00Z."));
            return;
        }

        var minimum = GetDateTimeSetting(field, "minimumDateTime");
        if (minimum is { } min && moment < min)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.BelowMinimum,
                $"'{field.Name}' must be at or after {min:u}."));
        }

        var maximum = GetDateTimeSetting(field, "maximumDateTime");
        if (maximum is { } max && moment > max)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.AboveMaximum,
                $"'{field.Name}' must be at or before {max:u}."));
        }
    }

    private static void ValidateDropdown(FieldDefinition field, JsonElement value, List<RecordValidationError> errors)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(InvalidType(field, "a JSON string holding an option value", value));
            return;
        }

        var text = value.GetString() ?? string.Empty;
        if (!ActiveOptionValues(field).Contains(text))
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.UnknownOption,
                $"'{text}' is not an active option of '{field.Name}'."));
        }
    }

    private static void ValidateMultiSelect(FieldDefinition field, JsonElement value, List<RecordValidationError> errors)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            errors.Add(InvalidType(field, "a JSON array of option values", value));
            return;
        }

        var allowed = ActiveOptionValues(field);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;

        foreach (var item in value.EnumerateArray())
        {
            count++;

            if (item.ValueKind != JsonValueKind.String)
            {
                errors.Add(new RecordValidationError(
                    field.Key,
                    RecordValidationErrorCodes.InvalidType,
                    $"'{field.Name}' must contain only option value strings."));
                continue;
            }

            var text = item.GetString() ?? string.Empty;

            if (!allowed.Contains(text))
            {
                errors.Add(new RecordValidationError(
                    field.Key,
                    RecordValidationErrorCodes.UnknownOption,
                    $"'{text}' is not an active option of '{field.Name}'."));
                continue;
            }

            if (!seen.Add(text))
            {
                errors.Add(new RecordValidationError(
                    field.Key,
                    RecordValidationErrorCodes.DuplicateOption,
                    $"'{text}' is selected more than once in '{field.Name}'."));
            }
        }

        if (field.IsRequired && count == 0)
        {
            errors.Add(Required(field));
            return;
        }

        var minimumItems = GetInt32Setting(field, "minimumItems");
        if (minimumItems is { } min && count < min)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.TooFewItems,
                $"'{field.Name}' requires at least {min} selections."));
        }

        var maximumItems = GetInt32Setting(field, "maximumItems");
        if (maximumItems is { } max && count > max)
        {
            errors.Add(new RecordValidationError(
                field.Key,
                RecordValidationErrorCodes.TooManyItems,
                $"'{field.Name}' allows at most {max} selections."));
        }
    }

    private static HashSet<string> ActiveOptionValues(FieldDefinition field) =>
        field.Options.Where(o => o.IsActive).Select(o => o.Value).ToHashSet(StringComparer.Ordinal);

    private static RecordValidationError Required(FieldDefinition field) => new(
        field.Key,
        RecordValidationErrorCodes.Required,
        $"'{field.Name}' is required.");

    private static RecordValidationError InvalidType(FieldDefinition field, string expected, JsonElement value) => new(
        field.Key,
        RecordValidationErrorCodes.InvalidType,
        $"'{field.Name}' must be {expected} but was {Describe(value)}.");

    private static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Array => "an array",
        JsonValueKind.Object => "an object",
        _ => "null"
    };

    internal static bool TryParseDateTime(string text, out DateTimeOffset moment) =>
        DateTimeOffset.TryParseExact(
            text,
            DateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out moment);

    private static bool TryGetSetting(FieldDefinition field, string name, out JsonElement setting)
    {
        setting = default;

        if (!FieldConfiguration.Properties.ContainsKey(field.FieldType))
        {
            return false;
        }

        foreach (var property in FieldConfiguration.Settings(field).EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                setting = property.Value;
                return setting.ValueKind != JsonValueKind.Null;
            }
        }

        return false;
    }

    private static decimal? GetDecimalSetting(FieldDefinition field, string name) =>
        TryGetSetting(field, name, out var setting)
        && setting.ValueKind == JsonValueKind.Number
        && setting.TryGetDecimal(out var number)
            ? number
            : null;

    private static int? GetInt32Setting(FieldDefinition field, string name) =>
        TryGetSetting(field, name, out var setting)
        && setting.ValueKind == JsonValueKind.Number
        && setting.TryGetInt32(out var number)
            ? number
            : null;

    private static DateOnly? GetDateSetting(FieldDefinition field, string name) =>
        TryGetSetting(field, name, out var setting)
        && setting.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(
            setting.GetString(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;

    private static DateTimeOffset? GetDateTimeSetting(FieldDefinition field, string name) =>
        TryGetSetting(field, name, out var setting)
        && setting.ValueKind == JsonValueKind.String
        && TryParseDateTime(setting.GetString() ?? string.Empty, out var moment)
            ? moment
            : null;
}
