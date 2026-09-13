using handytool_api.Configuration;
using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace handytool_api.Endpoints;

public static partial class ObjectDefinitionEndpoints
{
    private static async Task<IResult> GetEditorAsync(long id, HttpContext http, AccessService access,
        DefinitionLoader loader, IOptions<LocalizationOptions> localization, CancellationToken ct)
    {
        var actor = await access.ActorAsync(http, ct);
        var definition = await loader.LoadAsync(id, actor, ct);
        if (definition is null) return Results.NotFound();
        if (!AccessService.CanManageDefinition(actor, definition)) return Results.Forbid();
        DefinitionUpdateService.CheckGraph(definition);
        return Results.Ok(ObjectDefinitionResponse.Editor(definition, localization.Value.DefaultLanguage));
    }

    private static async Task<IResult> UpdateAsync(long id, EditObjectDefinitionRequest input, HttpContext http,
        HandyToolDbContext db, AccessService access, DefinitionLoader loader,
        IOptions<LocalizationOptions> localization, CancellationToken ct)
    {
        if (input.Definition is null) throw new ApiFailure(400, "definition_required", "A definition is required.");
        var actor = await access.ActorAsync(http, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await DefinitionUpdateService.LockAsync(db, true, ct);
        var definition = await access.Definitions(actor).Include(d => d.Translations).SingleOrDefaultAsync(d => d.Id == id, ct);
        if (definition is null) return Results.NotFound();
        if (!AccessService.CanManageDefinition(actor, definition)) return Results.Forbid();
        DefinitionUpdateService.CheckVersion(definition, input.ModifiedDate);
        var request = input.Definition;
        var errors = ValidateShape(request);
        if (errors.Count > 0) return ApiResults.ValidationFailed("The definition is invalid.", errors);
        // Ownership and audience changes are a separate operation; edits must not broaden access or change quotas.
        if (request.Visibility != definition.Visibility || request.RequiredAccessLevel != definition.RequiredAccessLevel ||
            request.MasterCategoryId != definition.MasterCategoryId || request.SubcategoryId != definition.SubcategoryId)
            throw new ApiFailure(400, "definition_scope", "Preserve the definition's visibility, access level and categories.");
        if (await db.ObjectDefinitions.AnyAsync(d => d.Id != id && d.CreatedByUserId == definition.CreatedByUserId && d.Name == request.Name.Trim(), ct))
            throw new ApiFailure(409, "duplicate_name", "An object definition with that name already exists.");
        var now = DateTime.UtcNow;
        // PostgreSQL timestamps have microsecond precision; ensure a different token even for immediate consecutive edits.
        now = new DateTime(Math.Max(now.Ticks / 10 * 10, definition.ModifiedDate.Ticks + 10), DateTimeKind.Utc);
        definition.Fields = await FieldConfiguration.IncludeConfigurations(db.FieldDefinitions.Where(f => f.ObjectDefinitionId == id)).ToListAsync(ct);
        async Task LoadItems(FieldDefinition field, int depth)
        {
            if (depth > 8) throw new ApiFailure(400, "schema_too_complex", "Definition nesting exceeds the supported limit.");
            if (field.CollectionField is { } collection)
            {
                collection.ItemDefinition = await FieldConfiguration.IncludeConfigurations(db.FieldDefinitions).SingleAsync(f => f.Id == collection.ItemDefinitionId, ct);
                await LoadItems(collection.ItemDefinition, depth + 1);
            }
        }
        foreach (var field in definition.Fields) await LoadItems(field, 1);
        var proposed = new List<FieldDefinition>();
        foreach (var field in request.Fields ?? [])
            proposed.Add(await BuildFieldAsync(field, db, access, actor, localization.Value, now, 1, ct));
        DefinitionUpdateService.MergeFields(definition.Fields, proposed, request.Fields ?? [], now);
        definition.Name = request.Name.Trim(); definition.Description = request.Description?.Trim() ?? "";
        definition.IsActive = input.IsActive; definition.ModifiedDate = now;
        var translations = new Dictionary<string, ObjectDefinitionTranslation>();
        foreach (var (language, text) in Translations(request.NameTranslations, localization.Value, NameMaxLength))
            translations[language] = new() { LanguageCode = language, Name = text };
        foreach (var (language, text) in Translations(request.DescriptionTranslations, localization.Value, DescriptionMaxLength))
        {
            if (!translations.TryGetValue(language, out var row)) translations[language] = row = new() { LanguageCode = language };
            row.Description = text;
        }
        DefinitionUpdateService.SyncTranslations(definition.Translations, translations.Values, t => t.LanguageCode,
            (a,b) => { a.Name = b.Name; a.Description = b.Description; });
        await db.SaveChangesAsync(ct);
        await DefinitionUpdateService.CheckStoredDataAsync(db, loader, id, ct);
        var saved = (await loader.LoadAsync(id, actor, ct))!;
        await transaction.CommitAsync(ct);
        return Results.Ok(ObjectDefinitionResponse.Editor(saved, localization.Value.DefaultLanguage));
    }
}

