using System.Text.Json;
using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Security;
using handytool_api.Services;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Endpoints;

public static class ObjectRecordEndpoints
{
    public static void MapObjectRecordEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Records").WithRateLimit(RateLimitPolicies.General).AddEndpointFilter<ApiFailureFilter>();
        group.MapGet("/object-definitions/{definitionId:long}/records", ListAsync);
        group.MapPost("/object-definitions/{definitionId:long}/records", CreateAsync);
        group.MapGet("/records/{id:long}", GetAsync);
        group.MapPut("/records/{id:long}", UpdateAsync);
        group.MapDelete("/records/{id:long}", DeleteAsync);
        group.MapGet("/access", async (HttpContext http, AccessService access, TrialIdentity trial, HandyToolDbContext db, CancellationToken ct) =>
        {
            var actor = await access.ActorAsync(http,ct);
            var device = actor.UserId == null ? trial.Get(http) : null;
            var used = device == null ? 0 : (await db.TrialUsage.FindAsync(["device:"+device],ct))?.CreatedCount ?? 0;
            return Results.Ok(new { actor.UserId, actor.CompanyId, actor.Role, actor.SuperAdmin, actor.Level, actor.Seats,
                actor.CanCreateDefinition, DefinitionLimit = PlanLimits.Definitions(actor), MonthlyRecordLimit = PlanLimits.Records(actor),
                TrialRemaining = actor.UserId == null ? Math.Max(0,7-used) : (int?)null });
        });
    }
    private static async Task<IResult> ListAsync(long definitionId,HttpContext http,AccessService access,TrialIdentity trial,
        CancellationToken ct,int skip=0,int take=50)
    {
        var a=await access.ActorAsync(http,ct);
        var q=access.Records(a,a.UserId==null?trial.Get(http):null).AsNoTracking().Where(r=>r.ObjectDefinitionId==definitionId);
        skip=Math.Max(0,skip);take=Math.Clamp(take,1,200);
        var count=await q.LongCountAsync(ct);
        var rows=await q.OrderByDescending(r=>r.CreatedDate).ThenByDescending(r=>r.Id).Skip(skip).Take(take).ToListAsync(ct);
        return Results.Ok(new PagedResponse<ObjectRecordResponse>(rows.Select(ObjectRecordResponse.From).ToList(),skip,take,count));
    }
    private static async Task<IResult> GetAsync(long id,HttpContext http,AccessService access,TrialIdentity trial,CancellationToken ct)
    {
        var a=await access.ActorAsync(http,ct);
        var r=await access.Records(a,a.UserId==null?trial.Get(http):null).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
        return r==null?Results.NotFound():Results.Ok(ObjectRecordResponse.From(r));
    }
    private static JsonElement Values(SaveObjectRecordRequest request) =>
        request.Values.ValueKind==JsonValueKind.Undefined?JsonSerializer.SerializeToElement(new {}):request.Values;
    private static void ValidateEnvelope(SaveObjectRecordRequest r,AccessActor a)
    {
        if(r.Title?.Length>300 || r.Description?.Length>4000) throw new ApiFailure(400,"too_long","Title or description exceeds its length limit.");
        if(!Enum.IsDefined(r.Visibility) || (r.Visibility==RecordVisibility.Company && a.CompanyId==null))
            throw new ApiFailure(400,"invalid_visibility","Company visibility requires company membership.");
        if(Values(r).GetRawText().Length>1_000_000) throw new ApiFailure(413,"record_too_large","Record exceeds the size limit.");
    }
    private static async Task<IResult> CreateAsync(long definitionId,SaveObjectRecordRequest request,HttpContext http,
        HandyToolDbContext db,AccessService access,DefinitionLoader loader,TrialIdentity trial,TrialQuota trials,CancellationToken ct)
    {
        var a=await access.ActorAsync(http,ct);
        ValidateEnvelope(request,a);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        if(a.UserId!=null) { await access.LockAsync(a,ct); await access.CheckQuotaAsync(a,false,ct); }
        var d=await loader.LoadAsync(definitionId,a,ct);
        if(d==null) return Results.NotFound();
        if(!d.IsActive) throw new ApiFailure(400,"definition_inactive","Definition is inactive.");
        var values=Values(request);
        var validation=RecordValueValidator.Validate(d.Fields.ToList(),values);
        if(!validation.IsValid) return ApiResults.ValidationFailed("The record is invalid.",validation.Errors.ToArray());
        var device=a.UserId==null?trial.Get(http):null;
        if(device!=null) await trials.ConsumeAsync(http,device,ct);
        var now=DateTime.UtcNow;
        var r=new ObjectRecord { ObjectDefinitionId=d.Id,CreatedByUserId=a.UserId,CompanyId=a.CompanyId,AnonymousDeviceHash=device,
            Title=Clean(request.Title),Description=Clean(request.Description),Visibility=request.Visibility,
            Values=JsonDocument.Parse(values.GetRawText()),CreatedDate=now,ModifiedDate=now };
        db.ObjectRecords.Add(r);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return Results.Created($"/api/records/{r.Id}",ObjectRecordResponse.From(r));
    }
    private static async Task<IResult> UpdateAsync(long id,SaveObjectRecordRequest request,HttpContext http,
        HandyToolDbContext db,AccessService access,DefinitionLoader loader,TrialIdentity trial,CancellationToken ct)
    {
        var a=await access.ActorAsync(http,ct);var device=a.UserId==null?trial.Get(http):null;
        var r=await access.Records(a,device).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null)return Results.NotFound();
        if(!AccessService.CanWriteRecord(a,r,device))return Results.Forbid();
        // Validate against the record's owning company, not a SuperAdmin's own membership.
        ValidateEnvelope(request,a with { CompanyId=r.CompanyId });
        if(request.Revision==null)throw new ApiFailure(400,"revision_required","Include the revision loaded with this record.");
        if(request.Revision!=r.Revision)throw new ApiFailure(409,"revision_conflict","This record changed. Reload before saving.");
        var d=await loader.LoadAsync(r.ObjectDefinitionId,a,ct);
        if(d==null)throw new ApiFailure(403,"definition_unavailable","The record's definition is unavailable.");
        var values=Values(request);var validation=RecordValueValidator.Validate(d.Fields.ToList(),values);
        if(!validation.IsValid)return ApiResults.ValidationFailed("The record is invalid.",validation.Errors.ToArray());
        r.Title=Clean(request.Title);r.Description=Clean(request.Description);r.Values=JsonDocument.Parse(values.GetRawText());
        r.Visibility=request.Visibility;r.Revision=checked(r.Revision+1);r.ModifiedDate=DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ObjectRecordResponse.From(r));
    }
    private static async Task<IResult> DeleteAsync(long id,HttpContext http,HandyToolDbContext db,AccessService access,TrialIdentity trial,CancellationToken ct,long? revision=null)
    {
        var a=await access.ActorAsync(http,ct);var device=a.UserId==null?trial.Get(http):null;
        var r=await access.Records(a,device).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null)return Results.NotFound();
        if(!AccessService.CanWriteRecord(a,r,device))return Results.Forbid();
        if(revision==null)throw new ApiFailure(400,"revision_required","Include the revision when deleting.");
        if(revision!=r.Revision)throw new ApiFailure(409,"revision_conflict","This record changed. Reload before deleting.");
        db.ObjectRecords.Remove(r);await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
    private static string? Clean(string? text)=>string.IsNullOrWhiteSpace(text)?null:text.Trim();
}
