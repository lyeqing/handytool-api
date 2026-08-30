using System.Text.Json;
using System.Text.RegularExpressions;
using handytool_api.Contracts;
using handytool_api.Configuration;
using handytool_api.Data;
using handytool_api.Localization;
using handytool_api.Models;
using handytool_api.Security;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace handytool_api.Endpoints;

/// <summary>
/// Metadata endpoints: users design their own object types here. These endpoints write
/// <see cref="ObjectDefinition"/>, <see cref="FieldDefinition"/> and <see cref="FieldOption"/> rows -
/// never a new table and never a new C# type.
/// </summary>
public static partial class ObjectDefinitionEndpoints
{
    // Mirrors the HasMaxLength values in the EF configurations. Translations are held to the same
    // limits as the columns they shadow, so a length cap cannot be sidestepped via jsonb.
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 2000;
    private const int LabelMaxLength = 200;

    public static void MapObjectDefinitionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/object-definitions")
            .WithTags("Object definitions")
            .RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.General);

        group.MapGet("/", ListAsync)
            .WithName("ListObjectDefinitions")
            .WithSummary("List the caller's object definitions")
            .WithDescription("Summaries only - fields and options are not included.")
            .Produces<List<ObjectDefinitionResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:long}", GetAsync)
            .WithName("GetObjectDefinition")
            .WithSummary("Read one object definition with its fields and options")
            .WithDescription("Includes inactive fields and options so the schema can be edited.")
            .Produces<ObjectDefinitionResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", CreateAsync)
            .WithName("CreateObjectDefinition")
            .WithSummary("Design a new object type")
            .WithDescription(
                "Creates metadata rows only - no table and no C# type is generated. Field keys and " +
                "option values are the stable identifiers stored in record JSON, so choose them carefully.")
            .Produces<ObjectDefinitionResponse>(StatusCodes.Status201Created)
            .Produces<ValidationErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ValidationErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        HandyToolDbContext db,
        IOptions<LocalizationOptions> localization,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        var definitions = await db.ObjectDefinitions
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        var language = RequestLanguage.Resolve(httpContext, localization.Value);

        return Results.Ok(definitions.Select(d => ObjectDefinitionResponse.Summary(d, language)).ToList());
    }

    private static async Task<IResult> GetAsync(
        long id,
        HttpContext httpContext,
        HandyToolDbContext db,
        IOptions<LocalizationOptions> localization,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        var definition = await db.ObjectDefinitions
            .AsNoTracking()
            .Include(d => d.Fields)
            .ThenInclude(f => f.Options)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, cancellationToken);

        if (definition is null)
        {
            return Results.NotFound();
        }

        // The editing view: this endpoint already returns inactive fields so a schema can be edited,
        // so it returns the translation maps for the same reason.
        var language = RequestLanguage.Resolve(httpContext, localization.Value);

        return Results.Ok(ObjectDefinitionResponse.ForEditing(definition, language));
    }

    /// <summary>Log category for these endpoints - static classes cannot be used as ILogger&lt;T&gt;.</summary>
    private const string LogCategory = "handytool_api.Endpoints.ObjectDefinitions";

    private static async Task<IResult> CreateAsync(
        CreateObjectDefinitionRequest request,
        HttpContext httpContext,
        HandyToolDbContext db,
        IOptions<LocalizationOptions> localization,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);
        var options = localization.Value;

        if (!CurrentUser.TryGetUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        var errors = ValidateShape(request);
        if (errors.Count > 0)
        {
            logger.LogInformation(
                "Rejected object definition {DefinitionName} for user {UserId}: {ErrorCodes}",
                request.Name,
                userId,
                errors.Select(e => e.ErrorCode).Distinct());
            return ApiResults.ValidationFailed("The object definition is invalid.", errors);
        }

        var nameTaken = await db.ObjectDefinitions
            .AnyAsync(d => d.UserId == userId && d.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            return Results.Conflict(new ValidationErrorResponse(
                "An object definition with that name already exists.",
                [new RecordValidationError("name", "duplicate_name", $"'{request.Name}' is already in use.")]));
        }

        var now = DateTime.UtcNow;

        var definition = new ObjectDefinition
        {
            UserId = userId,
            Name = request.Name.Trim(),
            NameTranslations = LocalizedText.Sanitise(request.NameTranslations, options, NameMaxLength),
            Description = request.Description?.Trim() ?? string.Empty,
            DescriptionTranslations = LocalizedText.Sanitise(request.DescriptionTranslations, options, DescriptionMaxLength),
            IsActive = true,
            CreatedDate = now,
            ModifiedDate = now
        };

        foreach (var fieldRequest in request.Fields ?? [])
        {
            var field = new FieldDefinition
            {
                Key = fieldRequest.Key.Trim(),
                Name = fieldRequest.Name.Trim(),
                NameTranslations = LocalizedText.Sanitise(fieldRequest.NameTranslations, options, NameMaxLength),
                Description = fieldRequest.Description?.Trim(),
                DescriptionTranslations = LocalizedText.Sanitise(fieldRequest.DescriptionTranslations, options, DescriptionMaxLength),
                FieldType = fieldRequest.FieldType,
                IsRequired = fieldRequest.IsRequired,
                IsActive = true,
                DisplayOrder = fieldRequest.DisplayOrder,
                Settings = ToSettingsDocument(fieldRequest.Settings),
                CreatedDate = now,
                ModifiedDate = now
            };

            foreach (var optionRequest in fieldRequest.Options ?? [])
            {
                field.Options.Add(new FieldOption
                {
                    Value = optionRequest.Value.Trim(),
                    Label = optionRequest.Label.Trim(),
                    LabelTranslations = LocalizedText.Sanitise(optionRequest.LabelTranslations, options, LabelMaxLength),
                    DisplayOrder = optionRequest.DisplayOrder,
                    IsActive = true,
                    CreatedDate = now,
                    ModifiedDate = now
                });
            }

            definition.Fields.Add(field);
        }

        db.ObjectDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created object definition {DefinitionId} '{DefinitionName}' with {FieldCount} fields for user {UserId}",
            definition.Id,
            definition.Name,
            definition.Fields.Count,
            userId);

        return Results.Created(
            $"/api/object-definitions/{definition.Id}",
            ObjectDefinitionResponse.ForEditing(definition, options.DefaultLanguage));
    }

    /// <summary>
    /// Structural checks on the metadata itself. Dynamic record values are validated separately by
    /// <see cref="RecordValueValidator"/>.
    /// </summary>
    private static List<RecordValidationError> ValidateShape(CreateObjectDefinitionRequest request)
    {
        var errors = new List<RecordValidationError>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new RecordValidationError("name", RecordValidationErrorCodes.Required, "Name is required."));
        }
        else if (request.Name.Trim().Length > 200)
        {
            errors.Add(new RecordValidationError("name", RecordValidationErrorCodes.TooLong, "Name must be at most 200 characters."));
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in request.Fields ?? [])
        {
            var key = field.Key?.Trim() ?? string.Empty;

            if (!FieldKeyPattern().IsMatch(key))
            {
                errors.Add(new RecordValidationError(
                    key,
                    RecordValidationErrorCodes.InvalidFormat,
                    $"Field key '{key}' must start with a letter and contain only letters, digits and underscores (max 100)."));
            }
            else if (!seenKeys.Add(key))
            {
                errors.Add(new RecordValidationError(key, "duplicate_key", $"Field key '{key}' is used more than once."));
            }

            if (string.IsNullOrWhiteSpace(field.Name))
            {
                errors.Add(new RecordValidationError(key, RecordValidationErrorCodes.Required, $"Field '{key}' needs a display name."));
            }

            if (!Enum.IsDefined(field.FieldType))
            {
                errors.Add(new RecordValidationError(key, RecordValidationErrorCodes.InvalidType, $"Field '{key}' has an unknown field type."));
            }

            if (field.Settings is { ValueKind: not JsonValueKind.Object and not JsonValueKind.Undefined and not JsonValueKind.Null })
            {
                errors.Add(new RecordValidationError(key, RecordValidationErrorCodes.InvalidType, $"Settings for '{key}' must be a JSON object."));
            }

            var options = field.Options ?? [];
            var supportsOptions = field.FieldType is FieldType.Dropdown or FieldType.MultiSelect;

            if (options.Count > 0 && !supportsOptions)
            {
                errors.Add(new RecordValidationError(
                    key,
                    "options_not_supported",
                    $"Field '{key}' is a {field.FieldType} field and cannot have options."));
            }

            var seenValues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in options)
            {
                var value = option.Value?.Trim() ?? string.Empty;

                if (value.Length == 0)
                {
                    errors.Add(new RecordValidationError(key, RecordValidationErrorCodes.Required, $"An option of '{key}' has an empty value."));
                }
                else if (!seenValues.Add(value))
                {
                    errors.Add(new RecordValidationError(key, "duplicate_option_value", $"Option value '{value}' is repeated in '{key}'."));
                }

                if (string.IsNullOrWhiteSpace(option.Label))
                {
                    errors.Add(new RecordValidationError(key, RecordValidationErrorCodes.Required, $"Option '{value}' of '{key}' needs a label."));
                }
            }
        }

        return errors;
    }

    private static JsonDocument ToSettingsDocument(JsonElement? settings) =>
        settings is { ValueKind: JsonValueKind.Object } element
            ? JsonDocument.Parse(element.GetRawText())
            : FieldSettings.Empty();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]{0,99}$")]
    private static partial Regex FieldKeyPattern();
}
