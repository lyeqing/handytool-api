using System.Text.Json;
using System.Text.RegularExpressions;
using handytool_api.Configuration;
using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Security;
using handytool_api.Services;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace handytool_api.Endpoints;

/// <summary>
/// Metadata endpoints: users design their own object types here. These endpoints write
/// <see cref="ObjectDefinition"/>, <see cref="FieldDefinition"/>, <see cref="FieldOption"/> and their
/// translation rows - never a new table and never a new C# type.
/// </summary>
public static partial class ObjectDefinitionEndpoints
{
    // Mirrors the HasMaxLength values in the EF configurations. Translations are held to the same
    // limits as the columns they shadow, so a length cap cannot be sidestepped via a translation row.
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 2000;
    private const int LabelMaxLength = 200;

    /// <summary>Log category for these endpoints - static classes cannot be used as ILogger&lt;T&gt;.</summary>
    private const string LogCategory = "handytool_api.Endpoints.ObjectDefinitions";

    public static void MapObjectDefinitionEndpoints(this IEndpointRouteBuilder routes)
    {
        // Not RequireAuthorization: a Public definition is readable by anonymous trial visitors, the
        // same audience POST /api/object-definitions/{id}/records already serves. AccessService does
        // the filtering, so an anonymous caller sees public definitions and nothing else.
        var group = routes.MapGroup("/api/object-definitions")
            .WithTags("Object definitions")
            .WithRateLimit(RateLimitPolicies.General)
            .AddEndpointFilter<ApiFailureFilter>();

        group.MapGet("/", ListAsync)
            .WithName("ListObjectDefinitions")
            .WithSummary("List the object definitions the caller may read")
            .WithDescription("Summaries only - fields and options are not included.")
            .Produces<List<ObjectDefinitionResponse>>();

        group.MapGet("/{id:long}", GetAsync)
            .WithName("GetObjectDefinition")
            .WithSummary("Read one object definition with its fields and options")
            .WithDescription(
                "Callers who may manage the definition also receive inactive fields and the translation " +
                "maps, so the schema can be edited.")
            .Produces<ObjectDefinitionResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .WithName("CreateObjectDefinition")
            .WithSummary("Design a new object type")
            .WithDescription(
                "Creates metadata rows only - no table and no C# type is generated. Field keys and " +
                "option values are the stable identifiers stored in record JSON, so choose them carefully.")
            .Produces<ObjectDefinitionResponse>(StatusCodes.Status201Created)
            .Produces<ValidationErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ValidationErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        AccessService access,
        IOptions<LocalizationOptions> localization,
        CancellationToken cancellationToken,
        long? categoryId = null,
        long? subcategoryId = null)
    {
        var actor = await access.ActorAsync(httpContext, cancellationToken);
        var language = RequestLanguage.Resolve(httpContext, localization.Value);

        var definitions = await access.Definitions(actor)
            .AsNoTracking()
            .Where(d => categoryId == null || d.MasterCategoryId == categoryId)
            .Where(d => subcategoryId == null || d.SubcategoryId == subcategoryId)
            .Include(d => d.Translations)
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(definitions.Select(d => ObjectDefinitionResponse.Summary(d, language)).ToList());
    }

    private static async Task<IResult> GetAsync(
        long id,
        HttpContext httpContext,
        AccessService access,
        DefinitionLoader loader,
        IOptions<LocalizationOptions> localization,
        CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(httpContext, cancellationToken);

        // The loader applies the same access filter and resolves nested Object and Collection fields.
        var definition = await loader.LoadAsync(id, actor, cancellationToken);
        if (definition is null)
        {
            return Results.NotFound();
        }

        var language = RequestLanguage.Resolve(httpContext, localization.Value);

        // The editing view carries the translation maps; a read-only caller has no use for them.
        return Results.Ok(AccessService.CanManageDefinition(actor, definition)
            ? ObjectDefinitionResponse.ForEditing(definition, language)
            : ObjectDefinitionResponse.WithFields(definition, language));
    }

    private static async Task<IResult> CreateAsync(
        CreateObjectDefinitionRequest request,
        HttpContext httpContext,
        HandyToolDbContext db,
        AccessService access,
        DefinitionLoader loader,
        IOptions<LocalizationOptions> localization,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);
        var options = localization.Value;

        var actor = await access.ActorAsync(httpContext, cancellationToken);
        if (!actor.CanCreateDefinition)
        {
            throw new ApiFailure(403, "definition_create_denied",
                "Only a signed-in user, or a company owner or admin, may design object types.");
        }

        var errors = ValidateShape(request);
        if (errors.Count > 0)
        {
            logger.LogInformation(
                "Rejected object definition {DefinitionName} for user {UserId}: {ErrorCodes}",
                request.Name,
                actor.UserId,
                errors.Select(e => e.ErrorCode).Distinct());
            return ApiResults.ValidationFailed("The object definition is invalid.", errors);
        }

        if (!Enum.IsDefined(request.Visibility))
        {
            throw new ApiFailure(400, "invalid_visibility", "Unknown visibility.");
        }

        // Public definitions are the shared catalogue; only a SuperAdmin publishes into it.
        if (request.Visibility == DefinitionVisibility.Public && !actor.SuperAdmin)
        {
            throw new ApiFailure(403, "publish_denied", "Only an administrator may publish a public definition.");
        }

        if (request.Visibility == DefinitionVisibility.Company && actor.CompanyId is null)
        {
            throw new ApiFailure(400, "invalid_visibility", "Company visibility requires company membership.");
        }

        if (request.RequiredAccessLevel is < 0 or > 3)
        {
            throw new ApiFailure(400, "invalid_access_level", "Required access level must be between 0 and 3.");
        }

        await ValidateCategoryAsync(db, request, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Lock before counting, so two concurrent creates cannot both pass the quota check.
        var quotaActor = AccessService.DefinitionQuotaActor(actor, request.Visibility);
        await access.LockAsync(quotaActor, cancellationToken);
        await access.CheckQuotaAsync(quotaActor, definitions: true, cancellationToken);

        var name = request.Name.Trim();
        var nameTaken = await db.ObjectDefinitions
            .AnyAsync(d => d.CreatedByUserId == actor.UserId && d.Name == name, cancellationToken);
        if (nameTaken)
        {
            return Results.Conflict(new ValidationErrorResponse(
                "An object definition with that name already exists.",
                [new RecordValidationError("name", "duplicate_name", $"'{name}' is already in use.")]));
        }

        var now = DateTime.UtcNow;

        var definition = new ObjectDefinition
        {
            CreatedByUserId = actor.UserId!.Value,
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
            MasterCategoryId = request.MasterCategoryId,
            SubcategoryId = request.SubcategoryId,
            Visibility = request.Visibility,
            RequiredAccessLevel = request.RequiredAccessLevel,
            // Company visibility names the owning company explicitly; personal definitions have none.
            CompanyId = quotaActor.CompanyId,
            IsActive = true,
            CreatedDate = now,
            ModifiedDate = now
        };

        foreach (var (language, text) in Translations(request.NameTranslations, options, NameMaxLength))
        {
            definition.Translations.Add(new ObjectDefinitionTranslation { LanguageCode = language, Name = text });
        }

        foreach (var (language, text) in Translations(request.DescriptionTranslations, options, DescriptionMaxLength))
        {
            var row = definition.Translations.FirstOrDefault(t => t.LanguageCode == language);
            if (row is null)
            {
                definition.Translations.Add(new ObjectDefinitionTranslation { LanguageCode = language, Description = text });
            }
            else
            {
                row.Description = text;
            }
        }

        foreach (var fieldRequest in request.Fields ?? [])
        {
            definition.Fields.Add(await BuildFieldAsync(fieldRequest, db, access, actor, options, now, 1, cancellationToken));
        }

        db.ObjectDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);

        // Check the complete graph using the read path, including all referenced definitions.
        // A loader failure disposes the uncommitted transaction and rolls back this creation.
        var loadedDefinition = await loader.LoadAsync(definition.Id, actor, cancellationToken)
            ?? throw new ApiFailure(403, "definition_unavailable", "The created definition is not accessible.");
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Created object definition {DefinitionId} '{DefinitionName}' with {FieldCount} fields for user {UserId}",
            definition.Id,
            definition.Name,
            definition.Fields.Count,
            actor.UserId);

        return Results.Created(
            $"/api/object-definitions/{definition.Id}",
            ObjectDefinitionResponse.ForEditing(loadedDefinition, options.DefaultLanguage));
    }

    /// <summary>
    /// Builds one field plus its typed configuration row, translations and options. Nested Collection
    /// item definitions recurse; the depth cap mirrors <see cref="DefinitionLoader"/> so a schema that
    /// can be written can also be read back.
    /// </summary>
    private static async Task<FieldDefinition> BuildFieldAsync(
        CreateFieldDefinitionRequest request,
        HandyToolDbContext db,
        AccessService access,
        AccessActor actor,
        LocalizationOptions options,
        DateTime now,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > 8)
        {
            throw new ApiFailure(400, "schema_too_complex", "Definition nesting exceeds the supported limit.");
        }

        var field = new FieldDefinition
        {
            Key = request.Key.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            FieldType = request.FieldType,
            IsRequired = request.IsRequired,
            IsActive = true,
            DisplayOrder = request.DisplayOrder,
            CreatedDate = now,
            ModifiedDate = now
        };

        // Apply validates the settings object and attaches the one typed configuration row this
        // field type requires. Every other configuration navigation stays null.
        var configuration = FieldConfiguration.Apply(field, request.Settings);

        AddFieldTranslations(field, request, options);

        switch (request.FieldType)
        {
            case FieldType.Collection:
            {
                if (request.Item is null)
                {
                    throw new ApiFailure(400, "item_required", $"Collection field '{field.Key}' needs an item definition.");
                }

                var collection = (CollectionField)configuration;
                if (collection.ItemDefinitionId != 0)
                {
                    throw new ApiFailure(400, "invalid_settings",
                        $"Collection field '{field.Key}' takes an item definition, not an itemDefinitionId setting.");
                }

                // A standalone definition: ObjectDefinitionId stays null, so it belongs to no object
                // type and is reached only through this collection.
                var item = await BuildFieldAsync(request.Item, db, access, actor, options, now, depth + 1, cancellationToken);
                db.FieldDefinitions.Add(item);
                collection.ItemDefinition = item;
                break;
            }

            case FieldType.Object:
            {
                var reference = (ObjectField)configuration;
                if (reference.ReferencedObjectDefinitionId == 0)
                {
                    throw new ApiFailure(400, "invalid_settings",
                        $"Object field '{field.Key}' needs a referencedObjectDefinitionId setting.");
                }

                // Referencing a definition the caller cannot read would leak its shape through the
                // editing view, so the reference is checked against the same access filter.
                var readable = await access.Definitions(actor)
                    .AnyAsync(d => d.Id == reference.ReferencedObjectDefinitionId, cancellationToken);
                if (!readable)
                {
                    throw new ApiFailure(403, "nested_definition_unavailable",
                        $"Object field '{field.Key}' references a definition that is not accessible.");
                }

                break;
            }
        }

        foreach (var optionRequest in request.Options ?? [])
        {
            var option = new FieldOption
            {
                Value = optionRequest.Value.Trim(),
                Label = optionRequest.Label.Trim(),
                DisplayOrder = optionRequest.DisplayOrder,
                IsActive = true,
                CreatedDate = now,
                ModifiedDate = now
            };

            foreach (var (language, text) in Translations(optionRequest.LabelTranslations, options, LabelMaxLength))
            {
                option.Translations.Add(new FieldOptionTranslation { LanguageCode = language, Label = text });
            }

            field.Options.Add(option);
        }

        return field;
    }

    private static void AddFieldTranslations(
        FieldDefinition field,
        CreateFieldDefinitionRequest request,
        LocalizationOptions options)
    {
        // One row per language carries all three translated strings, so they are gathered first.
        var rows = new Dictionary<string, FieldDefinitionTranslation>(StringComparer.Ordinal);

        FieldDefinitionTranslation Row(string language)
        {
            if (!rows.TryGetValue(language, out var row))
            {
                row = new FieldDefinitionTranslation { LanguageCode = language };
                rows[language] = row;
                field.Translations.Add(row);
            }

            return row;
        }

        foreach (var (language, text) in Translations(request.NameTranslations, options, NameMaxLength))
        {
            Row(language).Name = text;
        }

        foreach (var (language, text) in Translations(request.DescriptionTranslations, options, DescriptionMaxLength))
        {
            Row(language).Description = text;
        }

        foreach (var (language, text) in Translations(request.PlaceholderTranslations, options, DescriptionMaxLength))
        {
            Row(language).Placeholder = text;
        }
    }

    /// <summary>
    /// Normalises a submitted translation map: unsupported languages are dropped rather than stored,
    /// tags are folded to their configured spelling so "ZH-HANS" and "zh-hans" cannot become two rows,
    /// and each value is held to the same limit as the column it shadows.
    /// </summary>
    private static IEnumerable<(string Language, string Text)> Translations(
        IReadOnlyDictionary<string, string>? translations,
        LocalizationOptions options,
        int maxLength)
    {
        if (translations is null)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (rawLanguage, rawText) in translations)
        {
            if (options.Normalise(rawLanguage) is not { } language || !seen.Add(language))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                continue;
            }

            var text = rawText.Trim();
            if (text.Length > maxLength)
            {
                throw new ApiFailure(400, RecordValidationErrorCodes.TooLong,
                    $"The {language} translation must be at most {maxLength} characters.");
            }

            yield return (language, text);
        }
    }

    private static async Task ValidateCategoryAsync(
        HandyToolDbContext db,
        CreateObjectDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        // A missing category would otherwise surface as a foreign-key violation, which is a 500.
        var categoryExists = await db.MasterCategories
            .AnyAsync(c => c.Id == request.MasterCategoryId && c.IsActive, cancellationToken);
        if (!categoryExists)
        {
            throw new ApiFailure(400, "unknown_category", "That master category does not exist.");
        }

        if (request.SubcategoryId is not { } subcategoryId)
        {
            return;
        }

        // The composite foreign key already refuses a mismatched pair; checking here turns that into
        // a 400 with a reason rather than a database error.
        var subcategoryExists = await db.Subcategories.AnyAsync(
            s => s.Id == subcategoryId && s.MasterCategoryId == request.MasterCategoryId && s.IsActive,
            cancellationToken);
        if (!subcategoryExists)
        {
            throw new ApiFailure(400, "unknown_subcategory",
                "That subcategory does not exist, or does not belong to the chosen master category.");
        }
    }

    /// <summary>
    /// Structural checks on the metadata itself. Field settings are validated by
    /// <see cref="FieldConfiguration.Apply"/>, and dynamic record values by
    /// <see cref="RecordValueValidator"/>.
    /// </summary>
    private static List<RecordValidationError> ValidateShape(CreateObjectDefinitionRequest request)
    {
        var errors = new List<RecordValidationError>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new RecordValidationError("name", RecordValidationErrorCodes.Required, "Name is required."));
        }
        else if (request.Name.Trim().Length > NameMaxLength)
        {
            errors.Add(new RecordValidationError("name", RecordValidationErrorCodes.TooLong, $"Name must be at most {NameMaxLength} characters."));
        }

        if (request.Description?.Trim().Length > DescriptionMaxLength)
        {
            errors.Add(new RecordValidationError("description", RecordValidationErrorCodes.TooLong, $"Description must be at most {DescriptionMaxLength} characters."));
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var fieldCount = 0;

        foreach (var field in request.Fields ?? [])
        {
            ValidateField(field, seenKeys, errors, 1, ref fieldCount);
        }

        return errors;
    }

    private static void ValidateField(
        CreateFieldDefinitionRequest field,
        HashSet<string>? seenKeys,
        List<RecordValidationError> errors,
        int depth,
        ref int fieldCount)
    {
        if (depth > 8 || ++fieldCount > 512)
        {
            throw new ApiFailure(400, "schema_too_complex", "Definition nesting exceeds the supported limit.");
        }

        var key = field.Key?.Trim() ?? string.Empty;

        if (!FieldKeyPattern().IsMatch(key))
        {
            errors.Add(new RecordValidationError(
                key,
                RecordValidationErrorCodes.InvalidFormat,
                $"Field key '{key}' must start with a letter and contain only letters, digits and underscores (max 100)."));
        }
        else if (seenKeys is not null && !seenKeys.Add(key))
        {
            errors.Add(new RecordValidationError(key, "duplicate_key", $"Field key '{key}' is used more than once."));
        }

        if (string.IsNullOrWhiteSpace(field.Name))
        {
            errors.Add(new RecordValidationError(key, RecordValidationErrorCodes.Required, $"Field '{key}' needs a display name."));
        }
        else if (field.Name.Trim().Length > NameMaxLength)
        {
            errors.Add(new RecordValidationError(key, RecordValidationErrorCodes.TooLong, $"Field '{key}' has a name longer than {NameMaxLength} characters."));
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
        var supportsOptions = field.FieldType is FieldType.Dropdown or FieldType.MultiSelect
            or FieldType.RadioGroup or FieldType.Checklist;

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
            else if (option.Label.Trim().Length > LabelMaxLength)
            {
                errors.Add(new RecordValidationError(key, RecordValidationErrorCodes.TooLong, $"Option '{value}' of '{key}' has a label longer than {LabelMaxLength} characters."));
            }
        }

        // A collection item is a standalone definition, so its key is checked in its own namespace
        // rather than against the object's field keys.
        if (field.Item is not null)
        {
            ValidateField(field.Item, null, errors, depth + 1, ref fieldCount);
        }
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]{0,99}$")]
    private static partial Regex FieldKeyPattern();
}
