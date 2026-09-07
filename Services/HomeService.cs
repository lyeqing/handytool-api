using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Services;

public sealed class HomeService(HandyToolDbContext db, AccessService access)
{
    public async Task<List<CategoryResponse>> CategoriesAsync(AccessActor actor, string language, CancellationToken ct)
    {
        var visible = access.Definitions(actor).Where(d => d.IsActive);
        var categories = await db.MasterCategories.AsNoTracking()
            .Where(c => c.IsActive && visible.Any(d => d.MasterCategoryId == c.Id))
            .Include(c => c.Translations).Include(c => c.Subcategories).ThenInclude(s => s.Translations)
            .OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync(ct);
        var subcategoryIds = await visible.Where(d => d.SubcategoryId != null)
            .Select(d => d.SubcategoryId!.Value).Distinct().ToListAsync(ct);
        return categories.Select(c => new CategoryResponse(c.Id,
            c.Translations.FirstOrDefault(t => t.LanguageCode == language)?.Name ?? c.Name,
            c.Translations.FirstOrDefault(t => t.LanguageCode == language)?.Description ?? c.Description,
            c.Subcategories.Where(s => s.IsActive && subcategoryIds.Contains(s.Id)).OrderBy(s => s.Name).ThenBy(s => s.Id)
                .Select(s => new SubcategoryResponse(s.Id,
                    s.Translations.FirstOrDefault(t => t.LanguageCode == language)?.Name ?? s.Name,
                    s.Translations.FirstOrDefault(t => t.LanguageCode == language)?.Description ?? s.Description)).ToList())).ToList();
    }

    public async Task<PagedResponse<HomeRecordResponse>> RecordsAsync(AccessActor actor, string language,
        long? categoryId, long? subcategoryId, int skip, int take, CancellationToken ct)
    {
        if (actor.UserId is null) throw new ApiFailure(401, "sign_in_required", "Please sign in.");
        (skip, take) = PageBounds(skip, take);
        var query = access.Records(actor, null).AsNoTracking().Where(r => r.CreatedByUserId == actor.UserId);
        if (categoryId is { } category) query = query.Where(r => r.ObjectDefinition.MasterCategoryId == category);
        if (subcategoryId is { } subcategory) query = query.Where(r => r.ObjectDefinition.SubcategoryId == subcategory);
        var total = await query.LongCountAsync(ct);
        var rows = await query.OrderByDescending(r => r.ModifiedDate).ThenByDescending(r => r.Id)
            .Skip(skip).Take(take).Select(r => new HomeRecordResponse(r.Id, r.ObjectDefinitionId,
                r.ObjectDefinition.Translations.Where(t => t.LanguageCode == language).Select(t => t.Name).FirstOrDefault()
                    ?? r.ObjectDefinition.Name, r.Title, r.Description, r.ModifiedDate)).ToListAsync(ct);
        return new(rows, skip, take, total);
    }

    public static (int Skip, int Take) PageBounds(int skip, int take) => (Math.Max(0, skip), Math.Clamp(take, 1, 50));

    public async Task<HomeResponse> LoadAsync(AccessActor actor, string language, long? categoryId,
        long? subcategoryId, int skip, CancellationToken ct)
    {
        HomeUserResponse? user = actor.UserId is { } id
            ? await db.UserAccounts.Where(u => u.Id == id).Select(u => new HomeUserResponse(u.Id, u.DisplayName)).SingleAsync(ct)
            : null;
        var categories = await CategoriesAsync(actor, language, ct);
        var query = access.Definitions(actor).AsNoTracking().Where(d => d.IsActive && d.MasterCategory.IsActive);
        if (categoryId is { } category) query = query.Where(d => d.MasterCategoryId == category);
        if (subcategoryId is { } subcategory) query = query.Where(d => d.SubcategoryId == subcategory);
        var tools = await query.Include(d => d.Translations).OrderBy(d => d.Name).ThenBy(d => d.Id).ToListAsync(ct);
        var records = user is null ? null : await RecordsAsync(actor, language, categoryId, subcategoryId, skip, 12, ct);
        return new(user, actor.CanCreateDefinition, categories,
            tools.Select(d => ObjectDefinitionResponse.Summary(d, language)).ToList(), records);
    }
}
