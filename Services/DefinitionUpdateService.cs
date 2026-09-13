using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Services;

/// <summary>Identity-preserving metadata updates and validation of stored records.</summary>
public static class DefinitionUpdateService
{
    // Exclusive for schema edits, shared for record writers. The transaction owns the lock.
    public static Task LockAsync(HandyToolDbContext db, bool schema, CancellationToken ct) =>
        schema
            ? db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('definition-schema', 0))", ct)
            : db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock_shared(hashtextextended('definition-schema', 0))", ct);

    public static void CheckVersion(ObjectDefinition definition, DateTime modifiedDate)
    {
        if (modifiedDate == default || modifiedDate.ToUniversalTime() != definition.ModifiedDate.ToUniversalTime())
            throw new ApiFailure(409, "definition_conflict", "This definition changed. Reload before saving.");
    }

    public static void MergeFields(ICollection<FieldDefinition> existing, IReadOnlyList<FieldDefinition> proposed,
        IReadOnlyList<CreateFieldDefinitionRequest> requests, DateTime now)
    {
        var ids = requests.Where(r => r.Id != null).Select(r => r.Id!.Value).ToList();
        if (ids.Distinct().Count() != ids.Count || existing.Any(f => !ids.Contains(f.Id)))
            throw new ApiFailure(400, "field_identity", "Retain existing fields and their IDs; deactivate a field instead of removing it.");
        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i]; var next = proposed[i];
            if (request.Id is null)
            {
                if (existing.Any(f => f.Key == next.Key))
                    throw new ApiFailure(400, "field_identity", "An existing key requires its original field ID.");
                existing.Add(next); continue;
            }
            var current = existing.SingleOrDefault(f => f.Id == request.Id)
                ?? throw new ApiFailure(400, "field_identity", "A field does not belong to this definition.");
            MergeField(current, next, request, now);
        }
    }

    public static void MergeField(FieldDefinition current, FieldDefinition next, CreateFieldDefinitionRequest request, DateTime now)
    {
        if (current.Key != next.Key || current.FieldType != next.FieldType)
            throw new ApiFailure(400, "stable_field", "Existing field keys and types cannot be changed. Add a new field instead.");
        current.Name = next.Name; current.Description = next.Description;
        current.IsActive = request.IsActive; current.IsRequired = next.IsRequired;
        current.DisplayOrder = next.DisplayOrder; current.ModifiedDate = now;
        SyncTranslations(current.Translations, next.Translations, t => t.LanguageCode, (a,b) => {
            a.Name = b.Name; a.Description = b.Description; a.Placeholder = b.Placeholder;
        });
        var navigation = FieldConfiguration.Properties[current.FieldType];
        var config = navigation.GetValue(current)!;
        var replacement = navigation.GetValue(next)!;
        foreach (var property in config.GetType().GetProperties())
        {
            if (property.Name is "FieldDefinitionId" or "ItemDefinitionId") continue;
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (property.CanWrite && (type.IsValueType || type == typeof(string)))
                property.SetValue(config, property.GetValue(replacement));
        }
        if (current.CollectionField is { } collection)
        {
            if (request.Item?.Id != collection.ItemDefinitionId)
                throw new ApiFailure(400, "field_identity", "Retain the collection item's original ID.");
            MergeField(collection.ItemDefinition, next.CollectionField!.ItemDefinition, request.Item, now);
        }
        var optionIds = (request.Options ?? []).Where(o => o.Id != null).Select(o => o.Id!.Value).ToList();
        if (optionIds.Distinct().Count() != optionIds.Count || current.Options.Any(o => !optionIds.Contains(o.Id)))
            throw new ApiFailure(400, "option_identity", "Retain existing options and their IDs; deactivate an option instead of removing it.");
        var nextOptions = next.Options.ToList();
        for (var i = 0; i < nextOptions.Count; i++)
        {
            var input = request.Options![i]; var option = nextOptions[i];
            if (input.Id is null) { current.Options.Add(option); continue; }
            var old = current.Options.SingleOrDefault(o => o.Id == input.Id)
                ?? throw new ApiFailure(400, "option_identity", "An option does not belong to this field.");
            if (old.Value != option.Value)
                throw new ApiFailure(400, "stable_option", "Existing option values cannot change. Add an option instead.");
            old.Label = option.Label; old.DisplayOrder = option.DisplayOrder;
            old.IsActive = input.IsActive; old.ModifiedDate = now;
            SyncTranslations(old.Translations, option.Translations, t => t.LanguageCode, (a,b) => a.Label = b.Label);
        }
    }

    public static void SyncTranslations<T>(ICollection<T> current, IEnumerable<T> proposed, Func<T,string> key, Action<T,T> copy)
        where T : class
    {
        var desired = proposed.ToDictionary(key);
        foreach (var old in current.ToList())
        {
            if (!desired.TryGetValue(key(old), out var next)) current.Remove(old);
            else { copy(old, next); desired.Remove(key(old)); }
        }
        foreach (var next in desired.Values) current.Add(next);
    }

    public static void CheckGraph(ObjectDefinition definition)
    {
        var stack = new HashSet<long>(); var count = 0;
        void Object(ObjectDefinition d, int depth)
        {
            if (!stack.Add(d.Id)) throw new ApiFailure(400, "definition_cycle", "Object references cannot form a cycle.");
            foreach (var field in d.Fields) Field(field, depth + 1);
            stack.Remove(d.Id);
        }
        void Field(FieldDefinition field, int depth)
        {
            if (depth > 8 || ++count > 512) throw new ApiFailure(400, "schema_too_complex", "Definition nesting exceeds the supported limit.");
            if (field.ObjectField?.ReferencedObjectDefinition is { } child) Object(child, depth);
            if (field.CollectionField?.ItemDefinition is { } item) Field(item, depth + 1);
        }
        Object(definition, 0);
    }

    public static bool References(ObjectDefinition definition, long id)
    {
        var visited = new HashSet<long>();
        bool Object(ObjectDefinition d) => d.Id == id || (visited.Add(d.Id) && d.Fields.Any(Field));
        bool Field(FieldDefinition f) => (f.ObjectField?.ReferencedObjectDefinition is { } d && Object(d)) ||
            (f.CollectionField?.ItemDefinition is { } item && Field(item));
        return Object(definition);
    }

    public static void CheckRecord(ObjectDefinition definition, ObjectRecord record)
    {
        if (!RecordValueValidator.Validate(definition.Fields.ToList(), record.Values.RootElement).IsValid)
            throw new ApiFailure(409, "incompatible_definition", "These changes would invalidate existing records. Existing data has not been changed.");
    }

    public static async Task CheckStoredDataAsync(HandyToolDbContext db, DefinitionLoader loader, long editedId, CancellationToken ct)
    {
        // Validate all dependent roots, including records owned by other users. Never return their contents.
        var actor = new AccessActor(null, null, null, true, 3, 1);
        var ids = await db.ObjectDefinitions.AsNoTracking().Select(d => d.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            var definition = (await loader.LoadAsync(id, actor, ct))!;
            if (!References(definition, editedId)) continue;
            CheckGraph(definition);
            if (id != editedId && !FindActive(definition, editedId))
                throw new ApiFailure(409, "referenced_definition", "A referenced definition must remain active.");
            var records = await db.ObjectRecords.AsNoTracking().Where(r => r.ObjectDefinitionId == id).ToListAsync(ct);
            foreach (var record in records) CheckRecord(definition, record);
        }
    }

    private static bool FindActive(ObjectDefinition root, long id)
    {
        var visited = new HashSet<long>();
        bool Object(ObjectDefinition d) => d.Id == id ? d.IsActive : visited.Add(d.Id) && d.Fields.Any(Field);
        bool Field(FieldDefinition f) => f.ObjectField?.ReferencedObjectDefinition is { } d && Object(d) ||
            f.CollectionField?.ItemDefinition is { } item && Field(item);
        return Object(root);
    }
}

