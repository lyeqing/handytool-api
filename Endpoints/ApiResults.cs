using handytool_api.Contracts;
using handytool_api.Security;
using handytool_api.Validation;

namespace handytool_api.Endpoints;

internal static class ApiResults
{
    public static IResult ValidationFailed(string title, IReadOnlyList<RecordValidationError> errors) =>
        Results.BadRequest(new ValidationErrorResponse(title, errors));

    public static IResult ValidationFailed(string title, params RecordValidationError[] errors) =>
        ValidationFailed(title, (IReadOnlyList<RecordValidationError>)errors);

    public static IResult MissingOwner() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Owner could not be determined.",
        detail: $"Send a positive integer '{CurrentOwner.HeaderName}' header. This is a placeholder until authentication is added.");
}
