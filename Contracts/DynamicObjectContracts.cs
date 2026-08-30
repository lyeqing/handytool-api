using System.Text.Json;
using System.Text.Json.Serialization;
using handytool_api.Localization;
using handytool_api.Models;
using handytool_api.Validation;

namespace handytool_api.Contracts;

// ---------- Requests ----------

public sealed record CreateFieldOptionRequest(
    string Value,
    string Label,
    int DisplayOrder = 0,
    // Language to translated label, for example {"zh-Hans": "水损"}. Unsupported languages are
    // dropped rather than stored. The Value is never translated.
    IReadOnlyDictionary<string, string>? LabelTranslations = null);

public sealed record CreateFieldDefinitionRequest(
    string Key,
    string Name,
    FieldType FieldType,
    string? Description = null,
    bool IsRequired = false,
    int DisplayOrder = 0,
    JsonElement? Settings = null,
    IReadOnlyList<CreateFieldOptionRequest>? Options = null,
    IReadOnlyDictionary<string, string>? NameTranslations = null,
    IReadOnlyDictionary<string, string>? DescriptionTranslations = null);

public sealed record CreateObjectDefinitionRequest(
    string Name,
    string? Description = null,
    IReadOnlyList<CreateFieldDefinitionRequest>? Fields = null,
    IReadOnlyDictionary<string, string>? NameTranslations = null,
    IReadOnlyDictionary<string, string>? DescriptionTranslations = null);

public sealed record SaveObjectRecordRequest(
    string Title,
    JsonElement Values,
    string? Description = null);

// ---------- Responses ----------

/// <summary>
/// <paramref name="Label"/> is already resolved for the request language - the caller never sees the
/// translation map unless it asked for the editing view. <paramref name="Value"/> is the stable
/// identifier and is identical in every language.
/// </summary>
public sealed record FieldOptionResponse(
    long Id,
    string Value,
    string Label,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyDictionary<string, string>? LabelTranslations = null)
{
    public static FieldOptionResponse From(FieldOption option, string language) => new(
        option.Id,
        option.Value,
        LocalizedText.Resolve(option.Label, option.LabelTranslations, language),
        option.DisplayOrder,
        option.IsActive);

    /// <summary>For the editing screen, which needs every language at once.</summary>
    public static FieldOptionResponse ForEditing(FieldOption option, string language) =>
        From(option, language) with { LabelTranslations = LocalizedText.ToDictionary(option.LabelTranslations) };
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
    IReadOnlyList<FieldOptionResponse> Options,
    IReadOnlyDictionary<string, string>? NameTranslations = null,
    IReadOnlyDictionary<string, string>? DescriptionTranslations = null)
{
    public static FieldDefinitionResponse From(FieldDefinition field, string language) => new(
        field.Id,
        field.Key,
        LocalizedText.Resolve(field.Name, field.NameTranslations, language),
        field.Description is null ? null : LocalizedText.Resolve(field.Description, field.DescriptionTranslations, language),
        field.FieldType,
        field.IsRequired,
        field.IsActive,
        field.DisplayOrder,
        field.Settings.RootElement.Clone(),
        field.Options.OrderBy(o => o.DisplayOrder).Select(o => FieldOptionResponse.From(o, language)).ToList());

    public static FieldDefinitionResponse ForEditing(FieldDefinition field, string language) =>
        From(field, language) with
        {
            NameTranslations = LocalizedText.ToDictionary(field.NameTranslations),
            DescriptionTranslations = LocalizedText.ToDictionary(field.DescriptionTranslations),
            Options = field.Options.OrderBy(o => o.DisplayOrder).Select(o => FieldOptionResponse.ForEditing(o, language)).ToList()
        };
}

public sealed record ObjectDefinitionResponse(
    long Id,
    long UserId,
    string Name,
    string Description,
    bool IsActive,
    DateTime CreatedDate,
    DateTime ModifiedDate,
    IReadOnlyList<FieldDefinitionResponse>? Fields = null,
    IReadOnlyDictionary<string, string>? NameTranslations = null,
    IReadOnlyDictionary<string, string>? DescriptionTranslations = null)
{
    public static ObjectDefinitionResponse Summary(ObjectDefinition definition, string language) => new(
        definition.Id,
        definition.UserId,
        LocalizedText.Resolve(definition.Name, definition.NameTranslations, language),
        LocalizedText.Resolve(definition.Description, definition.DescriptionTranslations, language),
        definition.IsActive,
        definition.CreatedDate,
        definition.ModifiedDate);

    /// <summary>
    /// The reading view: every label already resolved for one language, no translation maps. This is
    /// what the website renders.
    /// </summary>
    public static ObjectDefinitionResponse WithFields(ObjectDefinition definition, string language) =>
        Summary(definition, language) with
        {
            Fields = definition.Fields
                .OrderBy(f => f.DisplayOrder)
                .Select(f => FieldDefinitionResponse.From(f, language))
                .ToList()
        };

    /// <summary>
    /// The editing view: the same thing plus every translation, so a schema editor can show and
    /// change all languages at once.
    /// </summary>
    public static ObjectDefinitionResponse ForEditing(ObjectDefinition definition, string language) =>
        Summary(definition, language) with
        {
            NameTranslations = LocalizedText.ToDictionary(definition.NameTranslations),
            DescriptionTranslations = LocalizedText.ToDictionary(definition.DescriptionTranslations),
            Fields = definition.Fields
                .OrderBy(f => f.DisplayOrder)
                .Select(f => FieldDefinitionResponse.ForEditing(f, language))
                .ToList()
        };
}

public sealed record ObjectRecordResponse(
    long Id,
    long ObjectDefinitionId,
    long UserId,
    string Title,
    string Description,
    JsonElement Values,
    DateTime CreatedDate,
    DateTime ModifiedDate)
{
    public static ObjectRecordResponse From(ObjectRecord record) => new(
        record.Id,
        record.ObjectDefinitionId,
        record.UserId,
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
