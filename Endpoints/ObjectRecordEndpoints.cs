using System.Text.Json;
using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Security;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Endpoints;

/// <summary>
/// Data endpoints. Every write path here runs <see cref="RecordValueValidator"/> before persisting.
/// </summary>
public static class ObjectRecordEndpoints
{
    private static readonly JsonDocument EmptyValues = JsonDocument.Parse(RecordValues.EmptyJson);

    public static void MapObjectRecordEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/object-definitions/{definitionId:long}/records")
            .WithTags("Records")
            .MapDefinitionScopedRecordEndpoints();

        var records = routes.MapGroup("/api/records").WithTags("Records");

        records.MapGet("/{id:long}", GetAsync)
            .WithName("GetRecord")
            .WithSummary("Read one record")
            .Produces<ObjectRecordResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        records.MapPut("/{id:long}", UpdateAsync)
            .WithName("UpdateRecord")
            .WithSummary("Replace one record")
            .WithDescription("Values are validated against the definition's active fields before saving.")
            .Produces<ObjectRecordResponse>()
            .Produces<ValidationErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        records.MapDelete("/{id:long}", DeleteAsync)
            .WithName("DeleteRecord")
            .WithSummary("Delete one record")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static void MapDefinitionScopedRecordEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListRecords")
            .WithSummary("List the records of one object definition")
            .WithDescription("Newest first. `take` is clamped to 1-200.")
            .Produces<PagedResponse<ObjectRecordResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", CreateAsync)
            .WithName("CreateRecord")
            .WithSummary("Add a record to an object definition")
            .WithDescription(
                "`values` must be a JSON object keyed by field key, holding values only - never labels " +
                "or other metadata. Types are not coerced: \"8\" is rejected for a numeric field. " +
                "Dropdown and multi-select entries must be active FieldOption values.")
            .Produces<ObjectRecordResponse>(StatusCodes.Status201Created)
            .Produces<ValidationErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> ListAsync(
        long definitionId,
        HttpContext httpContext,
        HandyToolDbContext db,
        CancellationToken cancellationToken,
        int skip = 0,
        int take = 50)
    {
        if (!CurrentOwner.TryGetOwnerId(httpContext, out var ownerId))
        {
            return ApiResults.MissingOwner();
        }

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);

        var query = db.ObjectRecords
            .AsNoTracking()
            .Where(r => r.ObjectDefinitionId == definitionId && r.OwnerId == ownerId);

        var total = await query.LongCountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(r => r.CreatedDate)
            .ThenByDescending(r => r.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<ObjectRecordResponse>(
            records.Select(ObjectRecordResponse.From).ToList(),
            skip,
            take,
            total));
    }

    private static async Task<IResult> GetAsync(
        long id,
        HttpContext httpContext,
        HandyToolDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CurrentOwner.TryGetOwnerId(httpContext, out var ownerId))
        {
            return ApiResults.MissingOwner();
        }

        var record = await db.ObjectRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, cancellationToken);

        return record is null ? Results.NotFound() : Results.Ok(ObjectRecordResponse.From(record));
    }

    /// <summary>Log category for these endpoints - static classes cannot be used as ILogger&lt;T&gt;.</summary>
    private const string LogCategory = "handytool_api.Endpoints.ObjectRecords";

    private static async Task<IResult> CreateAsync(
        long definitionId,
        SaveObjectRecordRequest request,
        HttpContext httpContext,
        HandyToolDbContext db,
        RecordValueValidator validator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);

        if (!CurrentOwner.TryGetOwnerId(httpContext, out var ownerId))
        {
            return ApiResults.MissingOwner();
        }

        var definition = await db.ObjectDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId && d.OwnerId == ownerId, cancellationToken);

        if (definition is null)
        {
            return Results.NotFound();
        }

        if (!definition.IsActive)
        {
            return ApiResults.ValidationFailed(
                "The object definition is inactive.",
                new RecordValidationError(string.Empty, "definition_inactive", "New records cannot be added to an inactive definition."));
        }

        var values = Normalize(request.Values);

        var titleError = ValidateTitle(request.Title);
        var validation = await validator.ValidateAsync(definitionId, values, cancellationToken);

        if (titleError is not null || !validation.IsValid)
        {
            var failures = Combine(titleError, validation);
            logger.LogInformation(
                "Rejected record for definition {DefinitionId}, owner {OwnerId}: {FieldKeys} failed with {ErrorCodes}",
                definitionId,
                ownerId,
                failures.Select(e => e.FieldKey),
                failures.Select(e => e.ErrorCode).Distinct());
            return ApiResults.ValidationFailed("The record is invalid.", failures);
        }

        var now = DateTime.UtcNow;

        var record = new ObjectRecord
        {
            ObjectDefinitionId = definitionId,
            OwnerId = ownerId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Values = JsonDocument.Parse(values.GetRawText()),
            CreatedDate = now,
            ModifiedDate = now
        };

        db.ObjectRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created record {RecordId} on definition {DefinitionId} for owner {OwnerId}",
            record.Id,
            definitionId,
            ownerId);

        return Results.Created($"/api/records/{record.Id}", ObjectRecordResponse.From(record));
    }

    private static async Task<IResult> UpdateAsync(
        long id,
        SaveObjectRecordRequest request,
        HttpContext httpContext,
        HandyToolDbContext db,
        RecordValueValidator validator,
        CancellationToken cancellationToken)
    {
        if (!CurrentOwner.TryGetOwnerId(httpContext, out var ownerId))
        {
            return ApiResults.MissingOwner();
        }

        var record = await db.ObjectRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, cancellationToken);

        if (record is null)
        {
            return Results.NotFound();
        }

        var values = Normalize(request.Values);

        var titleError = ValidateTitle(request.Title);
        var validation = await validator.ValidateAsync(record.ObjectDefinitionId, values, cancellationToken);

        if (titleError is not null || !validation.IsValid)
        {
            return ApiResults.ValidationFailed("The record is invalid.", Combine(titleError, validation));
        }

        record.Title = request.Title.Trim();
        record.Description = request.Description?.Trim() ?? string.Empty;
        record.Values = JsonDocument.Parse(values.GetRawText());
        record.ModifiedDate = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ObjectRecordResponse.From(record));
    }

    private static async Task<IResult> DeleteAsync(
        long id,
        HttpContext httpContext,
        HandyToolDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);

        if (!CurrentOwner.TryGetOwnerId(httpContext, out var ownerId))
        {
            return ApiResults.MissingOwner();
        }

        var record = await db.ObjectRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, cancellationToken);

        if (record is null)
        {
            return Results.NotFound();
        }

        db.ObjectRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);

        // User data was destroyed - always worth a line.
        logger.LogInformation(
            "Deleted record {RecordId} on definition {DefinitionId} for owner {OwnerId}",
            id,
            record.ObjectDefinitionId,
            ownerId);

        return Results.NoContent();
    }

    /// <summary>A missing "values" property is treated as an empty object, not as a bad request.</summary>
    private static JsonElement Normalize(JsonElement values) =>
        values.ValueKind == JsonValueKind.Undefined ? EmptyValues.RootElement : values;

    private static RecordValidationError? ValidateTitle(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? new RecordValidationError("title", RecordValidationErrorCodes.Required, "Title is required.")
            : title.Trim().Length > 300
                ? new RecordValidationError("title", RecordValidationErrorCodes.TooLong, "Title must be at most 300 characters.")
                : null;

    private static List<RecordValidationError> Combine(RecordValidationError? titleError, RecordValidationResult validation)
    {
        var errors = new List<RecordValidationError>();

        if (titleError is not null)
        {
            errors.Add(titleError);
        }

        errors.AddRange(validation.Errors);
        return errors;
    }
}
