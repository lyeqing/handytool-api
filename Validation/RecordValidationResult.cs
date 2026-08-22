namespace handytool_api.Validation;

/// <summary>
/// One structured validation failure for a dynamic record value.
/// </summary>
/// <param name="FieldKey">The <see cref="Models.FieldDefinition.Key"/> the error belongs to, or "" for document-level errors.</param>
/// <param name="ErrorCode">Stable machine-readable code - safe for mobile clients to switch on.</param>
/// <param name="Message">Human-readable message.</param>
public sealed record RecordValidationError(string FieldKey, string ErrorCode, string Message);

public sealed class RecordValidationResult
{
    public static readonly RecordValidationResult Success = new([]);

    public RecordValidationResult(IReadOnlyList<RecordValidationError> errors) => Errors = errors;

    public IReadOnlyList<RecordValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}

/// <summary>Stable error codes returned by <see cref="RecordValueValidator"/>.</summary>
public static class RecordValidationErrorCodes
{
    public const string ValuesNotObject = "values_not_object";
    public const string UnknownField = "unknown_field";
    public const string Required = "required";
    public const string InvalidType = "invalid_type";
    public const string InvalidFormat = "invalid_format";
    public const string TooShort = "too_short";
    public const string TooLong = "too_long";
    public const string BelowMinimum = "below_minimum";
    public const string AboveMaximum = "above_maximum";
    public const string InvalidStep = "invalid_step";
    public const string UnknownOption = "unknown_option";
    public const string DuplicateOption = "duplicate_option";
    public const string TooFewItems = "too_few_items";
    public const string TooManyItems = "too_many_items";
}
