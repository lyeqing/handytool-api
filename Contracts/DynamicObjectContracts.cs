using System.Text.Json;
using System.Text.Json.Serialization;
using handytool_api.Models;
using handytool_api.Services;
using handytool_api.Validation;

namespace handytool_api.Contracts;

public sealed record CreateFieldOptionRequest(string Value, string Label, int DisplayOrder = 0,
    IReadOnlyDictionary<string,string>? LabelTranslations = null);
public sealed record CreateFieldDefinitionRequest(string Key, string Name, FieldType FieldType,
    string? Description = null, bool IsRequired = false, int DisplayOrder = 0, JsonElement? Settings = null,
    IReadOnlyList<CreateFieldOptionRequest>? Options = null,
    IReadOnlyDictionary<string,string>? NameTranslations = null,
    IReadOnlyDictionary<string,string>? DescriptionTranslations = null,
    IReadOnlyDictionary<string,string>? PlaceholderTranslations = null,
    CreateFieldDefinitionRequest? Item = null);
public sealed record CreateObjectDefinitionRequest(string Name, string? Description = null,
    IReadOnlyList<CreateFieldDefinitionRequest>? Fields = null,
    IReadOnlyDictionary<string,string>? NameTranslations = null,
    IReadOnlyDictionary<string,string>? DescriptionTranslations = null,
    long MasterCategoryId = MasterCategory.UncategorizedId, long? SubcategoryId = null,
    DefinitionVisibility Visibility = DefinitionVisibility.Private, int RequiredAccessLevel = 0);
public sealed record UpdateDefinitionRequest(string Name, string? Description, DefinitionVisibility Visibility,
    int RequiredAccessLevel = 0, bool IsActive = true);
public sealed record SaveObjectRecordRequest(string? Title = null, JsonElement Values = default,
    string? Description = null, long? Revision = null, RecordVisibility Visibility = RecordVisibility.Private);

public sealed record FieldOptionResponse(long Id, string Value, string Label, int DisplayOrder, bool IsActive,
    IReadOnlyDictionary<string,string>? LabelTranslations = null)
{
    public static FieldOptionResponse From(FieldOption o, string language) => new(o.Id,o.Value,
        o.Translations.FirstOrDefault(t => t.LanguageCode == language)?.Label ?? o.Label,o.DisplayOrder,o.IsActive);
    public static FieldOptionResponse ForEditing(FieldOption o, string language) =>
        From(o,language) with { LabelTranslations = o.Translations.ToDictionary(t=>t.LanguageCode,t=>t.Label) };
}
public sealed record FieldDefinitionResponse(long Id, string Key, string Name, string? Description, FieldType FieldType,
    bool IsRequired, bool IsActive, int DisplayOrder, JsonElement Settings, IReadOnlyList<FieldOptionResponse> Options,
    IReadOnlyDictionary<string,string>? NameTranslations = null,
    IReadOnlyDictionary<string,string>? DescriptionTranslations = null,
    IReadOnlyDictionary<string,string>? PlaceholderTranslations = null,
    FieldDefinitionResponse? Item = null, IReadOnlyList<FieldDefinitionResponse>? Fields = null)
{
    public static FieldDefinitionResponse From(FieldDefinition f, string language, int depth = 0)
    {
        var t = f.Translations.FirstOrDefault(x=>x.LanguageCode==language);
        var settings = FieldConfiguration.Settings(f);
        if (t?.Placeholder != null)
        {
            var map = settings.Deserialize<Dictionary<string,JsonElement>>()!;
            map["placeholder"] = JsonSerializer.SerializeToElement(t.Placeholder);
            settings = JsonSerializer.SerializeToElement(map);
        }
        return new(f.Id,f.Key,t?.Name ?? f.Name,t?.Description ?? f.Description,f.FieldType,
            f.IsRequired,f.IsActive,f.DisplayOrder,settings,
            f.Options.OrderBy(o=>o.DisplayOrder).ThenBy(o=>o.Id).Select(o=>FieldOptionResponse.From(o,language)).ToList(),
            Item: depth < 8 && f.CollectionField?.ItemDefinition is { } item ? From(item,language,depth+1) : null,
            Fields: depth < 8 && f.ObjectField?.ReferencedObjectDefinition is { } obj
                ? obj.Fields.OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.Id).Select(x=>From(x,language,depth+1)).ToList() : null);
    }
    public static FieldDefinitionResponse ForEditing(FieldDefinition f,string language) => From(f,language) with {
        NameTranslations = f.Translations.Where(t=>t.Name!=null).ToDictionary(t=>t.LanguageCode,t=>t.Name!),
        DescriptionTranslations = f.Translations.Where(t=>t.Description!=null).ToDictionary(t=>t.LanguageCode,t=>t.Description!),
        PlaceholderTranslations = f.Translations.Where(t=>t.Placeholder!=null).ToDictionary(t=>t.LanguageCode,t=>t.Placeholder!),
        Options = f.Options.OrderBy(o=>o.DisplayOrder).Select(o=>FieldOptionResponse.ForEditing(o,language)).ToList()
    };
}
public sealed record ObjectDefinitionResponse(long Id, long CreatedByUserId, string Name, string Description, bool IsActive,
    DateTime CreatedDate, DateTime ModifiedDate, long? CompanyId, DefinitionVisibility Visibility, int RequiredAccessLevel,
    long MasterCategoryId, long? SubcategoryId, IReadOnlyList<FieldDefinitionResponse>? Fields = null,
    IReadOnlyDictionary<string,string>? NameTranslations = null,
    IReadOnlyDictionary<string,string>? DescriptionTranslations = null)
{
    public static ObjectDefinitionResponse Summary(ObjectDefinition d,string language)
    {
        var t=d.Translations.FirstOrDefault(x=>x.LanguageCode==language);
        return new(d.Id,d.CreatedByUserId,t?.Name??d.Name,t?.Description??d.Description,d.IsActive,d.CreatedDate,d.ModifiedDate,
            d.CompanyId,d.Visibility,d.RequiredAccessLevel,d.MasterCategoryId,d.SubcategoryId);
    }
    public static ObjectDefinitionResponse WithFields(ObjectDefinition d,string language) => Summary(d,language) with {
        Fields=d.Fields.OrderBy(f=>f.DisplayOrder).ThenBy(f=>f.Id).Select(f=>FieldDefinitionResponse.From(f,language)).ToList()
    };
    public static ObjectDefinitionResponse ForEditing(ObjectDefinition d,string language) => Summary(d,language) with {
        Fields=d.Fields.OrderBy(f=>f.DisplayOrder).ThenBy(f=>f.Id).Select(f=>FieldDefinitionResponse.ForEditing(f,language)).ToList(),
        NameTranslations=d.Translations.Where(t=>t.Name!=null).ToDictionary(t=>t.LanguageCode,t=>t.Name!),
        DescriptionTranslations=d.Translations.Where(t=>t.Description!=null).ToDictionary(t=>t.LanguageCode,t=>t.Description!)
    };
}
public sealed record ObjectRecordResponse(long Id,long ObjectDefinitionId,long? CreatedByUserId,string? Title,string? Description,
    JsonElement Values,DateTime CreatedDate,DateTime ModifiedDate,long? CompanyId,long Revision,RecordVisibility Visibility)
{
    public static ObjectRecordResponse From(ObjectRecord r) => new(r.Id,r.ObjectDefinitionId,r.CreatedByUserId,r.Title,r.Description,
        r.Values.RootElement.Clone(),r.CreatedDate,r.ModifiedDate,r.CompanyId,r.Revision,r.Visibility);
}
public sealed record PagedResponse<T>(IReadOnlyList<T> Items,int Skip,int Take,long Total);
public sealed record ValidationErrorResponse(string Title,IReadOnlyList<RecordValidationError> Errors)
{
    [JsonPropertyName("status")] public int Status => 400;
}
