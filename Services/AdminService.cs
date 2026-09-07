using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Services;

public sealed class AdminService(HandyToolDbContext db, AccessService access, ILogger<AdminService> log)
{
    public static void RequireSuperuser(AccessActor actor)
    {
        if (actor.UserId is null) throw new ApiFailure(401, "sign_in_required", "Please sign in.");
        if (!actor.SuperAdmin) throw new ApiFailure(403, "forbidden", "Superuser access is required.");
    }
    public async Task AuthorizeAsync(HttpContext http, CancellationToken ct) => RequireSuperuser(await access.ActorAsync(http, ct));
    static void Invalid(string message) => throw new ApiFailure(400, "invalid_admin_input", message);
    public static string? Text(string? value, int max, bool required = false)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if ((required && value is null) || value?.Length > max) Invalid($"A required field is missing or exceeds {max} characters.");
        return value;
    }
    public static void ProtectLast(bool removing, int remaining, string role)
    {
        if (removing && remaining == 0) throw new ApiFailure(409, "last_administrator", $"Keep at least one active {role}.");
    }
    static void Revision(DateTime expected, DateTime actual)
    {
        if (expected != actual) throw new ApiFailure(409, "revision_conflict", "This entry changed. Reload before saving.");
    }
    async Task Plan(int? id, CancellationToken ct)
    {
        if (id is null || !await db.AccountTypes.AnyAsync(x => x.Id == id, ct)) Invalid("Choose an existing account plan.");
    }
    public async Task<object> Users(string? q, int skip, CancellationToken ct)
    {
        q = Text(q, 200);
        var query = db.UserAccounts.AsNoTracking();
        if (q is not null) query = query.Where(u => u.Email.Contains(q) || u.DisplayName.Contains(q));
        return new { items = await query.OrderBy(u => u.Id).Skip(Math.Max(0, skip)).Take(25)
            .Select(u => new AdminUserRow(u.Id,u.DisplayName,u.Email,u.Phone,u.PreferredLanguage,u.CompanyId,u.CompanyRole,u.AccountTypeId,u.IsActive,u.IsSuperAdmin,u.ModifiedDate)).ToListAsync(ct),
            total = await query.CountAsync(ct) };
    }
    public async Task<object> Companies(string? q, int skip, CancellationToken ct)
    {
        q = Text(q, 200);
        var query = db.CompanyAccounts.AsNoTracking();
        if (q is not null) query = query.Where(c => c.Name.Contains(q));
        return new { items = await query.OrderBy(c => c.Id).Skip(Math.Max(0,skip)).Take(25)
            .Select(c => new AdminCompanyRow(c.Id,c.Name,c.Country,c.Address,c.WebsiteUrl,c.AccountTypeId,c.SeatLimit,c.ExpiresAt,c.IsActive,c.ModifiedDate)).ToListAsync(ct),
            total = await query.CountAsync(ct) };
    }
    public async Task<object> Categories(bool sub, string? q, int skip, CancellationToken ct)
    {
        q = Text(q,200);
        if (sub)
        {
            var query = db.Subcategories.AsNoTracking().Where(c => q == null || c.Name.Contains(q));
            var rows = await query.Include(c => c.Translations).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).Skip(Math.Max(0,skip)).Take(25).ToListAsync(ct);
            return new { items = rows.Select(c => new AdminCategoryRow(c.Id,c.Name,c.Description,c.IsActive,c.DisplayOrder,c.MasterCategoryId,
                c.Translations.Select(t => new AdminTranslation(t.LanguageCode,t.Name,t.Description)).ToList(),c.ModifiedDate)), total = await query.CountAsync(ct) };
        }
        else
        {
            var query = db.MasterCategories.AsNoTracking().Where(c => q == null || c.Name.Contains(q));
            var rows = await query.Include(c => c.Translations).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).Skip(Math.Max(0,skip)).Take(25).ToListAsync(ct);
            return new { items = rows.Select(c => new AdminCategoryRow(c.Id,c.Name,c.Description,c.IsActive,c.DisplayOrder,null,
                c.Translations.Select(t => new AdminTranslation(t.LanguageCode,t.Name,t.Description)).ToList(),c.ModifiedDate)), total = await query.CountAsync(ct) };
        }
    }
    public async Task UpdateUser(long id, AdminUserEdit input, CancellationToken ct)
    {
        var u = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id,ct) ?? throw new ApiFailure(404,"not_found","User not found.");
        Revision(input.ModifiedDate,u.ModifiedDate);
        var name = Text(input.DisplayName,200,true)!;
        var email = Text(input.Email,320,true)!.ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(email,out var parsed) || parsed.Address != email) Invalid("Enter a valid email address.");
        await access.LockKeyAsync($"register:{email}",ct);
        if (await db.UserAccounts.AnyAsync(x => x.Id != id && x.Email == email,ct)) throw new ApiFailure(409,"duplicate_email","Email is already registered.");
        ProtectLast(u.IsActive && u.IsSuperAdmin && (!input.IsActive || !input.IsSuperAdmin),
            await db.UserAccounts.CountAsync(x => x.Id != id && x.IsActive && x.IsSuperAdmin,ct),"superuser");
        if (u.CompanyId is { } oldCompany && u.IsActive && u.CompanyRole == CompanyRole.Owner)
            ProtectLast(!input.IsActive || input.CompanyId != oldCompany || input.CompanyRole != CompanyRole.Owner,
                await db.UserAccounts.CountAsync(x => x.Id != id && x.CompanyId == oldCompany && x.IsActive && x.CompanyRole == CompanyRole.Owner,ct),"company Owner");
        if (input.CompanyId is { } companyId)
        {
            var c = await db.CompanyAccounts.SingleOrDefaultAsync(x => x.Id == companyId,ct) ?? throw new ApiFailure(400,"invalid_company","Company not found.");
            if (input.CompanyRole is null || !Enum.IsDefined(input.CompanyRole.Value) || input.AccountTypeId != null) Invalid("Company users need a valid role and inherit their company plan.");
            var members = await db.UserAccounts.CountAsync(x => x.Id != id && x.CompanyId == companyId && x.IsActive,ct);
            if (input.IsActive && members >= c.SeatLimit) throw new ApiFailure(409,"seat_limit","Increase the company seat limit before adding another active member.");
            if (input.IsActive && input.CompanyRole != CompanyRole.Owner && !await db.UserAccounts.AnyAsync(x => x.Id != id && x.CompanyId == companyId && x.IsActive && x.CompanyRole == CompanyRole.Owner,ct))
                Invalid("Assign an active Owner to the company first.");
        }
        else
        {
            if (input.CompanyRole != null) Invalid("Personal users cannot have a company role.");
            await Plan(input.AccountTypeId,ct);
        }
        var language = Text(input.PreferredLanguage,10);
        if (language is not null && language != "en" && language != "zh-Hans") Invalid("Choose English, Chinese, or browser default.");
        // Identity/access changes invalidate existing sessions; profile-only edits do not.
        if (u.Email != email || u.IsActive != input.IsActive || u.IsSuperAdmin != input.IsSuperAdmin ||
            u.CompanyId != input.CompanyId || u.CompanyRole != input.CompanyRole)
            await db.UserSessions.Where(s => s.UserId == id && s.RevokedDate == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedDate, DateTime.UtcNow),ct);
        u.DisplayName=name; u.Email=email; u.Phone=Text(input.Phone,40); u.PreferredLanguage=language;
        u.CompanyId=input.CompanyId; u.CompanyRole=input.CompanyRole; u.AccountTypeId=input.AccountTypeId;
        u.IsActive=input.IsActive; u.IsSuperAdmin=input.IsSuperAdmin; u.ModifiedDate=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        log.LogInformation("Admin updated user {TargetId}",id);
    }
    public async Task UpdateCompany(long id, AdminCompanyEdit input, CancellationToken ct)
    {
        var c = await db.CompanyAccounts.SingleOrDefaultAsync(x => x.Id == id,ct) ?? throw new ApiFailure(404,"not_found","Company not found.");
        Revision(input.ModifiedDate,c.ModifiedDate);
        await Plan(input.AccountTypeId,ct);
        if (input.SeatLimit < 1 || input.SeatLimit < await db.UserAccounts.CountAsync(x => x.CompanyId == id && x.IsActive,ct)) Invalid("Seat limit must cover all active members and be at least one.");
        if (input.IsActive && !await db.UserAccounts.AnyAsync(x => x.CompanyId == id && x.IsActive && x.CompanyRole == CompanyRole.Owner,ct)) Invalid("An active company needs an active Owner.");
        var website=Text(input.WebsiteUrl,2048);
        if (website is not null && (!Uri.TryCreate(website,UriKind.Absolute,out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || uri.UserInfo.Length != 0)) Invalid("Enter an http or https website.");
        c.Name=Text(input.Name,200,true)!; c.Country=Text(input.Country,100); c.Address=Text(input.Address,2000); c.WebsiteUrl=website;
        c.AccountTypeId=input.AccountTypeId; c.SeatLimit=input.SeatLimit; c.ExpiresAt=input.ExpiresAt?.ToUniversalTime(); c.IsActive=input.IsActive; c.ModifiedDate=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        log.LogInformation("Admin updated company {TargetId}",id);
    }
    public async Task SaveCategory(long? id, bool sub, AdminCategoryEdit input, CancellationToken ct)
    {
        var name=Text(input.Name,200,true)!; var description=Text(input.Description,2000) ?? "";
        var translations=input.Translations ?? [];
        if (translations.Count > 2 || translations.Select(t => t.LanguageCode).Distinct().Count() != translations.Count) Invalid("Supply each translation once.");
        foreach(var t in translations) { if (t.LanguageCode != "en" && t.LanguageCode != "zh-Hans") Invalid("Unsupported translation language."); Text(t.Name,200); Text(t.Description,2000); }
        if (id is not null && input.ModifiedDate is null) Invalid("Reload this category before saving.");
        if (sub)
        {
            if (input.MasterCategoryId is null || !await db.MasterCategories.AnyAsync(x => x.Id == input.MasterCategoryId,ct)) Invalid("Choose an existing master category.");
            if (await db.Subcategories.AnyAsync(x => x.Id != id && x.MasterCategoryId == input.MasterCategoryId && x.Name == name,ct)) throw new ApiFailure(409,"duplicate_category","Category name already exists.");
            var c=id is null ? new Subcategory { CreatedDate=DateTime.UtcNow,MasterCategoryId=input.MasterCategoryId!.Value } :
                await db.Subcategories.Include(x => x.Translations).SingleOrDefaultAsync(x => x.Id == id,ct) ?? throw new ApiFailure(404,"not_found","Category not found.");
            if(id is not null) Revision(input.ModifiedDate!.Value,c.ModifiedDate);
            if(id is not null && c.MasterCategoryId != input.MasterCategoryId)
            {
                // The parent is part of an alternate key. Move references transactionally using SQL.
                var definitions=await db.ObjectDefinitions.Where(x => x.SubcategoryId == id).Select(x => x.Id).ToListAsync(ct);
                await db.ObjectDefinitions.Where(x => definitions.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.SubcategoryId,(long?)null),ct);
                await db.Subcategories.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.MasterCategoryId,input.MasterCategoryId!.Value),ct);
                await db.ObjectDefinitions.Where(x => definitions.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.MasterCategoryId,input.MasterCategoryId!.Value).SetProperty(x => x.SubcategoryId,id),ct);
            }
            c.Name=name; c.Description=description; c.IsActive=input.IsActive; c.DisplayOrder=input.DisplayOrder; c.ModifiedDate=DateTime.UtcNow;
            foreach(var t in translations) {
                var row=c.Translations.SingleOrDefault(x => x.LanguageCode == t.LanguageCode);
                if(row is null) { row=new SubcategoryTranslation { LanguageCode=t.LanguageCode }; c.Translations.Add(row); }
                row.Name=Text(t.Name,200); row.Description=Text(t.Description,2000);
            }
            if(id is null) db.Subcategories.Add(c);
        }
        else
        {
            if(input.MasterCategoryId != null) Invalid("Master categories cannot have a parent.");
            if(id == MasterCategory.UncategorizedId && !input.IsActive) Invalid("The default category must stay active.");
            if(await db.MasterCategories.AnyAsync(x => x.Id != id && x.Name == name,ct)) throw new ApiFailure(409,"duplicate_category","Category name already exists.");
            var c=id is null ? new MasterCategory { CreatedDate=DateTime.UtcNow } :
                await db.MasterCategories.Include(x => x.Translations).SingleOrDefaultAsync(x => x.Id == id,ct) ?? throw new ApiFailure(404,"not_found","Category not found.");
            if(id is not null) Revision(input.ModifiedDate!.Value,c.ModifiedDate);
            c.Name=name; c.Description=description; c.IsActive=input.IsActive; c.DisplayOrder=input.DisplayOrder; c.ModifiedDate=DateTime.UtcNow;
            foreach(var t in translations) {
                var row=c.Translations.SingleOrDefault(x => x.LanguageCode == t.LanguageCode);
                if(row is null) { row=new MasterCategoryTranslation { LanguageCode=t.LanguageCode }; c.Translations.Add(row); }
                row.Name=Text(t.Name,200); row.Description=Text(t.Description,2000);
            }
            if(id is null) db.MasterCategories.Add(c);
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Admin saved category {TargetId} subcategory {IsSubcategory}",id,sub);
    }
}

