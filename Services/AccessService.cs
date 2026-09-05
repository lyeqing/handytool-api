using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Services;

public sealed class ApiFailure(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed class ApiFailureFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (ApiFailure e) { return Results.Problem(statusCode: e.Status, title: e.Message, extensions: new Dictionary<string, object?> { ["code"] = e.Code }); }
        catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode: 409, title: "This record changed. Reload it before saving.", extensions: new Dictionary<string, object?> { ["code"] = "revision_conflict" }); }
    }
}
public sealed record AccessActor(long? UserId, long? CompanyId, CompanyRole? Role, bool SuperAdmin, int Level, int Seats, bool CompanyActive = true)
{
    public bool Manager => Role is CompanyRole.Admin or CompanyRole.Owner;
    public bool CanCreateDefinition => SuperAdmin || (UserId != null && CompanyActive && (CompanyId == null || Manager));
}
public static class PlanLimits
{
    public static long Definitions(AccessActor a) => a.SuperAdmin ? long.MaxValue : (a.Level, a.CompanyId != null) switch
    {
        (>=3, true) => 100L * a.Seats, (>=3, false) => 100,
        (2, true) => 50, (2, false) => 20, (_, true) => 10, _ => 5
    };
    public static long Records(AccessActor a) => a.SuperAdmin ? long.MaxValue : (a.Level, a.CompanyId != null) switch
    {
        (>=3, true) => 1000L * a.Seats, (>=3, false) => 1000,
        (2, true) => 200L * a.Seats, (2, false) => 200, (_, true) => 50, _ => 30
    };
    public static DateTime MonthStart(DateTime utc) => new(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
}
public sealed class AccessService(HandyToolDbContext db)
{
    public async Task<AccessActor> ActorAsync(HttpContext http, CancellationToken ct)
    {
        var id = CurrentUser.GetUserIdOrNull(http);
        if (id == null) return new(null, null, null, false, 0, 0);
        var u = await db.UserAccounts.AsNoTracking().Include(x => x.AccountType)
            .Include(x => x.Company).ThenInclude(x => x!.AccountType).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is null || !u.IsActive) throw new ApiFailure(401, "sign_in_required", "Please sign in.");
        var company = u.Company;
        var level = company?.AccountType.AccessLevel ?? u.AccountType?.AccessLevel ?? 1;
        if (company?.ExpiresAt <= DateTime.UtcNow) level = 1;
        return new(u.Id, u.CompanyId, u.CompanyRole, u.IsSuperAdmin, level, company?.SeatLimit ?? 1, company?.IsActive ?? true);
    }
    public static bool CanReadDefinition(AccessActor a, ObjectDefinition d) =>
        a.SuperAdmin || (d.Visibility == DefinitionVisibility.Public && d.RequiredAccessLevel <= a.Level) ||
        (d.CompanyId == null ? a.UserId != null && d.CreatedByUserId == a.UserId :
            a.CompanyActive && a.CompanyId == d.CompanyId && (a.Manager || d.CreatedByUserId == a.UserId || d.Visibility == DefinitionVisibility.Company));
    public static bool CanManageDefinition(AccessActor a, ObjectDefinition d) =>
        a.SuperAdmin || (d.CompanyId == null ? a.UserId != null && d.CreatedByUserId == a.UserId : a.CompanyActive && a.CompanyId == d.CompanyId && a.Manager);
    public IQueryable<ObjectDefinition> Definitions(AccessActor a)
    {
        if (a.SuperAdmin) return db.ObjectDefinitions;
        return db.ObjectDefinitions.Where(d =>
            (d.Visibility == DefinitionVisibility.Public && d.RequiredAccessLevel <= a.Level) ||
            (d.CompanyId == null && a.UserId != null && d.CreatedByUserId == a.UserId) ||
            (d.CompanyId != null && a.CompanyActive && a.CompanyId == d.CompanyId &&
                (a.Manager || d.CreatedByUserId == a.UserId || d.Visibility == DefinitionVisibility.Company)));
    }
    public static bool CanReadRecord(AccessActor a, ObjectRecord r, string? device = null) =>
        a.SuperAdmin || (r.CompanyId != null ?
            a.CompanyActive && a.CompanyId == r.CompanyId && (a.Manager || r.CreatedByUserId == a.UserId || r.Visibility == RecordVisibility.Company) :
            (a.UserId != null && r.CreatedByUserId == a.UserId) || (a.UserId == null && device != null && r.AnonymousDeviceHash == device));
    public static bool CanWriteRecord(AccessActor a, ObjectRecord r, string? device = null) =>
        CanReadRecord(a,r,device) && (a.SuperAdmin || (r.CompanyId != null && a.Manager) ||
            (a.UserId != null && r.CreatedByUserId == a.UserId) || (a.UserId == null && device != null && r.AnonymousDeviceHash == device));
    public IQueryable<ObjectRecord> Records(AccessActor a, string? device)
    {
        if (a.SuperAdmin) return db.ObjectRecords;
        return db.ObjectRecords.Where(r =>
            (r.CompanyId != null && a.CompanyActive && r.CompanyId == a.CompanyId &&
                (a.Manager || r.CreatedByUserId == a.UserId || r.Visibility == RecordVisibility.Company)) ||
            (r.CompanyId == null && a.UserId != null && r.CreatedByUserId == a.UserId) ||
            (a.UserId == null && device != null && r.AnonymousDeviceHash == device));
    }
    public Task LockAsync(AccessActor a, CancellationToken ct) =>
        LockKeyAsync(a.CompanyId != null ? $"company:{a.CompanyId}" : $"user:{a.UserId}", ct);
    public Task LockKeyAsync(string key, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    public async Task CheckQuotaAsync(AccessActor a, bool definitions, CancellationToken ct)
    {
        if (a.SuperAdmin) return;
        if (!a.CompanyActive) throw new ApiFailure(403, "company_inactive", "This company account is inactive.");
        long used;
        long limit;
        if (definitions)
        {
            used = await db.ObjectDefinitions.CountAsync(d => a.CompanyId != null ? d.CompanyId == a.CompanyId : d.CompanyId == null && d.CreatedByUserId == a.UserId, ct);
            limit = PlanLimits.Definitions(a);
        }
        else
        {
            var month = PlanLimits.MonthStart(DateTime.UtcNow);
            var end = month.AddMonths(1);
            used = await db.ObjectRecords.CountAsync(r => r.CreatedDate >= month && r.CreatedDate < end &&
                (a.CompanyId != null ? r.CompanyId == a.CompanyId : r.CompanyId == null && r.CreatedByUserId == a.UserId), ct);
            limit = PlanLimits.Records(a);
        }
        if (used >= limit) throw new ApiFailure(403, "quota_exceeded", $"Your plan allows {limit} {(definitions ? "retained definitions" : "records per UTC calendar month")}.");
    }
}
public sealed class TrialIdentity(IDataProtectionProvider provider)
{
    public const string CookieName = "handytool_trial";
    private readonly IDataProtector protector = provider.CreateProtector("HandyTool.AnonymousTrial.v1");
    public string Get(HttpContext http)
    {
        if (http.Items.TryGetValue(CookieName, out var existing)) return (string)existing!;
        string? token = null;
        if (http.Request.Cookies.TryGetValue(CookieName, out var raw))
        {
            try { token = protector.Unprotect(raw); } catch (CryptographicException) { }
        }
        if (token is null || token.Length != 64)
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            http.Response.Cookies.Append(CookieName, protector.Protect(token), new CookieOptions {
                HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/", MaxAge = TimeSpan.FromDays(3650), IsEssential = true
            });
        }
        var hash = Hash(token); http.Items[CookieName] = hash; return hash;
    }
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
public sealed class TrialQuota(HandyToolDbContext db, AccessService access, IConfiguration config)
{
    public async Task ConsumeAsync(HttpContext http, string device, CancellationToken ct)
    {
        // A secondary daily IP limit slows cookie-reset abuse without limiting a whole office to seven records.
        var ipKey = $"ip:{DateTime.UtcNow:yyyyMMdd}:{TrialIdentity.Hash(ClientAddress.RemoteIpAddress(http))}";
        foreach (var (key, limit) in new[] { (ipKey, config.GetValue("Trials:DailyIpLimit", 70)), ($"device:{device}", 7) })
        {
            await access.LockKeyAsync(key, ct);
            var usage = await db.TrialUsage.FindAsync([key], ct);
            if (usage is null) { usage = new TrialUsage { Key = key }; db.TrialUsage.Add(usage); }
            if (usage.CreatedCount >= limit) throw new ApiFailure(429, "trial_limit", "Trial limit reached. Sign in to continue.");
            usage.CreatedCount++; usage.UpdatedAt = DateTime.UtcNow;
        }
    }
}
