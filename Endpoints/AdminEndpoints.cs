using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Services;
using handytool_api.Security;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Endpoints;
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group=routes.MapGroup("/api/admin").WithTags("Administration").RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.General).AddEndpointFilter<ApiFailureFilter>();
        group.AddEndpointFilter(async (context,next) =>
        {
            var http=context.HttpContext;
            http.Response.Headers.CacheControl="private, no-store";
            var service=http.RequestServices.GetRequiredService<AdminService>();
            var db=http.RequestServices.GetRequiredService<HandyToolDbContext>();
            if(HttpMethods.IsGet(http.Request.Method))
            {
                await service.AuthorizeAsync(http,http.RequestAborted);
                return await next(context);
            }
            await using var transaction=await db.Database.BeginTransactionAsync(http.RequestAborted);
            // Serialize admin changes and re-check privileges after taking the lock.
            await http.RequestServices.GetRequiredService<AccessService>().LockKeyAsync("admin:mutations",http.RequestAborted);
            await service.AuthorizeAsync(http,http.RequestAborted);
            var result=await next(context);
            await transaction.CommitAsync(http.RequestAborted);
            return result;
        });
        group.MapGet("/users",async (AdminService s,CancellationToken ct,string? q=null,int skip=0) => Results.Ok(await s.Users(q,skip,ct)));
        group.MapGet("/companies",async (AdminService s,CancellationToken ct,string? q=null,int skip=0) => Results.Ok(await s.Companies(q,skip,ct)));
        group.MapGet("/categories",async (AdminService s,CancellationToken ct,bool sub=false,string? q=null,int skip=0) => Results.Ok(await s.Categories(sub,q,skip,ct)));
        group.MapGet("/plans",async (HandyToolDbContext db,CancellationToken ct) => Results.Ok(await db.AccountTypes.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(ct)));
        group.MapPut("/users/{id:long}",async (long id,AdminUserEdit input,AdminService s,CancellationToken ct) => { await s.UpdateUser(id,input,ct); return Results.NoContent(); });
        group.MapPut("/companies/{id:long}",async (long id,AdminCompanyEdit input,AdminService s,CancellationToken ct) => { await s.UpdateCompany(id,input,ct); return Results.NoContent(); });
        group.MapPost("/categories",async (AdminCategoryEdit input,AdminService s,CancellationToken ct,bool sub=false) => { await s.SaveCategory(null,sub,input,ct); return Results.NoContent(); });
        group.MapPut("/categories/{id:long}",async (long id,AdminCategoryEdit input,AdminService s,CancellationToken ct,bool sub=false) => { await s.SaveCategory(id,sub,input,ct); return Results.NoContent(); });
    }
}

