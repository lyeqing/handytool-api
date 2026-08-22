using System.Text.Json;
using System.Text.Json.Serialization;
using handytool_api.Models;
using handytool_api.Validation;

namespace handytool_api.Contracts;

// ---------- Requests ----------

public sealed record CreateFieldOptionRequest(
    string Value,
    string Label,
    int DisplayOrder = 0);

public sealed record CreateFieldDefinitionRequest(
    string Key,
    string Name,
    FieldType FieldType,
    string? Description = null,
    bool IsRequired = false,
    int DisplayOrder = 0,
    JsonElement? Settings = null,
    IReadOnlyList<CreateFieldOptionRequest>? Options = null);

public sealed record CreateObjectDefinitionRequest(
    string Name,
    string? Description = null,
    IReadOnlyList<CreateFieldDefinitionRequest>? Fields = null);

public sealed record SaveObjectRecordRequest(
    string Title,
    JsonElement Values,
    string? Description = null);

// ---------- Responses ----------

public sealed record FieldOptionResponse(
    long Id,
    string Value,
    string Label,
    int DisplayOrder,
    bool IsActive)
{
    public static FieldOptionResponse From(FieldOption option) => new(
        option.Id,
        option.Value,
        option.Label,
        option.DisplayOrder,
        option.IsActive);
}

public sealed record FieldDefinitionResponse(
    long Id,
    string Key,
    string Name,
    string? Description,
    FieldType FieldType,
    bool IsRequired,
    bool IsActive,
    int DisplayOrder,
    JsonElement Settings,
    IReadOnlyList<FieldOptionResponse> Options)
{
    public static FieldDefinitionResponse From(FieldDefinition field) => new(
        field.Id,
        field.Key,
        field.Name,
        field.Description,
        field.FieldType,
        field.IsRequired,
        field.IsActive,
        field.DisplayOrder,
        field.Settings.RootElement.Clone(),
        field.Options.OrderBy(o => o.DisplayOrder).Select(FieldOptionResponse.From).ToList());
}

public sealed record ObjectDefinitionResponse(
    long Id,
    long OwnerId,
    string Name,
    string Description,
    bool IsActive,
    DateTime CreatedDate,
    DateTime ModifiedDate,
    IReadOnlyList<FieldDefinitionResponse>? Fields = null)
{
    public static ObjectDefinitionResponse Summary(ObjectDefinition definition) => new(
        definition.Id,
        definition.OwnerId,
        definition.Name,
        definition.Description,
        definition.IsActive,
        definition.CreatedDate,
        definition.ModifiedDate);

    public static ObjectDefinitionResponse WithFields(ObjectDefinition definition) => Summary(definition) with
    {
        Fields = definition.Fields
            .OrderBy(f => f.DisplayOrder)
            .Select(FieldDefinitionResponse.From)
            .ToList()
    };
}

public sealed record ObjectRecordResponse(
    long Id,
    long ObjectDefinitionId,
    long OwnerId,
    string Title,
    string Description,
    JsonElement Values,
    DateTime CreatedDate,
    DateTime ModifiedDate)
{
    public static ObjectRecordResponse From(ObjectRecord record) => new(
        record.Id,
        record.ObjectDefinitionId,
        record.OwnerId,
        record.Title,
        record.Description,
        record.Values.RootElement.Clone(),
        record.CreatedDate,
        record.ModifiedDate);
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Skip, int Take, long Total);

/// <summary>
/// Error envelope returned for both definition-shape errors and dynamic value errors, so mobile
/// clients only ever have to parse one failure shape.
/// </summary>
public sealed record ValidationErrorResponse(
    string Title,
    IReadOnlyList<RecordValidationError> Errors)
{
    [JsonPropertyName("status")]
    public int Status => StatusCodes.Status400BadRequest;
}
