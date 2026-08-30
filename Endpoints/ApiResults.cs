using handytool_api.Contracts;
using handytool_api.Validation;

namespace handytool_api.Endpoints;

internal static class ApiResults
{
    public static IResult ValidationFailed(string title, IReadOnlyList<RecordValidationError> errors) =>
        Results.BadRequest(new ValidationErrorResponse(title, errors));

    public static IResult ValidationFailed(string title, params RecordValidationError[] errors) =>
        ValidationFailed(title, (IReadOnlyList<RecordValidationError>)errors);
}
