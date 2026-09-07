using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using handytool_api.Data;
using handytool_api.Models;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Services;

/// <summary>Converts API settings to typed EF configuration rows; JSON is transport only.</summary>
public static class FieldConfiguration
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static readonly IReadOnlyDictionary<FieldType, PropertyInfo> Properties = Enum.GetValues<FieldType>()
        .ToDictionary(t => t, t => typeof(FieldDefinition).GetProperty(t + "Field")!);
    public static JsonElement Settings(FieldDefinition field)
    {
        var config = Properties[field.FieldType].GetValue(field);
        var values = new Dictionary<string, object?>();
        if (config != null)
        foreach (var p in config.GetType().GetProperties())
        {
            if (p.Name == "FieldDefinitionId" || !IsSetting(p)) continue;
            var name = JsonNamingPolicy.CamelCase.ConvertName(p.Name);
            if (field.FieldType is FieldType.MultiSelect or FieldType.Checklist)
                name = name.Replace("minimumSelections","minimumItems").Replace("maximumSelections","maximumItems");
            if (p.GetValue(config) is { } value) values[name] = value;
        }
        return JsonSerializer.SerializeToElement(values, Json);
    }
    public static object Apply(FieldDefinition field, JsonElement? settings)
    {
        if (!Properties.TryGetValue(field.FieldType, out var navigation)) throw new ApiFailure(400,"invalid_type","Unknown field type.");
        var config = Activator.CreateInstance(navigation.PropertyType)!;
        if (settings is { ValueKind: not JsonValueKind.Object and not JsonValueKind.Null and not JsonValueKind.Undefined })
            throw new ApiFailure(400,"invalid_settings","Field settings must be an object.");
        var seen = new HashSet<string>();
        if (settings is { ValueKind: JsonValueKind.Object } json)
        foreach (var setting in json.EnumerateObject())
        {
            var name = setting.Name;
            if (field.FieldType is FieldType.MultiSelect or FieldType.Checklist)
                name = name.Replace("minimumItems","minimumSelections").Replace("maximumItems","maximumSelections");
            var property = navigation.PropertyType.GetProperties().FirstOrDefault(p => p.Name != "FieldDefinitionId" && IsSetting(p) && JsonNamingPolicy.CamelCase.ConvertName(p.Name) == name);
            if (property is null || !seen.Add(name)) throw new ApiFailure(400,"invalid_settings",$"Unsupported or duplicate setting '{setting.Name}' for {field.FieldType}.");
            try
            {
                var value = setting.Value.Deserialize(property.PropertyType, Json);
                if (value is DateTime time)
                {
                    if (time.Kind == DateTimeKind.Unspecified) throw new JsonException("Date/time requires an offset.");
                    value = time.ToUniversalTime();
                }
                property.SetValue(config, value);
            }
            catch (Exception e) when (e is JsonException or FormatException or ArgumentException)
            { throw new ApiFailure(400,"invalid_settings",$"Invalid value for '{setting.Name}'."); }
        }
        foreach (var p in navigation.PropertyType.GetProperties().Where(IsSetting))
        {
            var value = p.GetValue(config);
            if (value is string text && text.Length > 2000) throw new ApiFailure(400,"invalid_settings","Placeholder is too long.");
            if (value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
                && p.Name is not ("ReferencedObjectDefinitionId" or "ItemDefinitionId"))
            {
                var n = Convert.ToDecimal(value);
                if ((p.Name is "Step" or "StepSeconds" or "Rows") && n <= 0 ||
                    (p.Name.EndsWith("Length") || p.Name.EndsWith("Items") || p.Name.EndsWith("Selections")) && n < 0)
                    throw new ApiFailure(400,"invalid_settings",$"Invalid {p.Name}.");
            }
            if (p.Name.StartsWith("Minimum") && value is IComparable min)
            {
                var max = navigation.PropertyType.GetProperty(p.Name.Replace("Minimum","Maximum"))?.GetValue(config);
                if (max != null && min.CompareTo(max) > 0) throw new ApiFailure(400,"invalid_settings","Minimum cannot exceed maximum.");
            }
        }
        navigation.SetValue(field, config);
        return config;
    }
    private static bool IsSetting(PropertyInfo p)
    {
        var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        return t.IsValueType || t == typeof(string);
    }
    public static IQueryable<FieldDefinition> IncludeConfigurations(IQueryable<FieldDefinition> query)
    {
        foreach (var property in Properties.Values) query = query.Include(property.Name);
        return query.Include(x => x.Translations).Include(x => x.Options).ThenInclude(x => x.Translations).AsSplitQuery();
    }
}
public sealed class DefinitionLoader(HandyToolDbContext db, AccessService access)
{
    public async Task<ObjectDefinition?> LoadAsync(long id, AccessActor actor, CancellationToken ct)
    {
        var objects = new Dictionary<long, ObjectDefinition>();
        var items = new Dictionary<long, FieldDefinition>();
        var count = 0;
        async Task<ObjectDefinition?> LoadObject(long objectId, int depth)
        {
            if (depth > 8 || count > 512) throw new ApiFailure(400,"schema_too_complex","Definition nesting exceeds the supported limit.");
            if (objects.TryGetValue(objectId, out var found)) return found;
            var result = await access.Definitions(actor).AsNoTracking().Include(x => x.Translations).SingleOrDefaultAsync(x => x.Id == objectId, ct);
            if (result == null) return null;
            objects[objectId] = result;
            result.Fields = await FieldConfiguration.IncludeConfigurations(db.FieldDefinitions.AsNoTracking().Where(x => x.ObjectDefinitionId == objectId)).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync(ct);
            count += result.Fields.Count;
            foreach (var field in result.Fields) await Expand(field, depth + 1);
            return result;
        }
        async Task Expand(FieldDefinition field, int depth)
        {
            if (depth > 8 || count > 512) throw new ApiFailure(400,"schema_too_complex","Definition nesting exceeds the supported limit.");
            if (field.ObjectField is { } obj)
                obj.ReferencedObjectDefinition = await LoadObject(obj.ReferencedObjectDefinitionId, depth)
                    ?? throw new ApiFailure(403,"nested_definition_unavailable","A nested object definition is not accessible.");
            if (field.CollectionField is { } collection)
            {
                if (!items.TryGetValue(collection.ItemDefinitionId, out var item))
                {
                    item = await FieldConfiguration.IncludeConfigurations(db.FieldDefinitions.AsNoTracking()).SingleAsync(x => x.Id == collection.ItemDefinitionId, ct);
                    items[item.Id] = item; count++; await Expand(item, depth + 1);
                }
                collection.ItemDefinition = item;
            }
        }
        return await LoadObject(id, 0);
    }
}
