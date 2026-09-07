using handytool_api.Configuration;
using handytool_api.Security;
using handytool_api.Services;
using Microsoft.Extensions.Options;

namespace handytool_api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Home and categories")
            .WithRateLimit(RateLimitPolicies.General).AddEndpointFilter<ApiFailureFilter>();
        // Even the anonymous response depends on session state. Never share it across visitors.
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "private, no-store";
            return await next(context);
        });
        group.MapGet("/categories", async (HttpContext http, AccessService access, HomeService home,
            IOptions<LocalizationOptions> localization, CancellationToken ct) =>
            Results.Ok(await home.CategoriesAsync(await access.ActorAsync(http, ct),
                RequestLanguage.Resolve(http, localization.Value), ct)));
        group.MapGet("/home", async (HttpContext http, AccessService access, HomeService home,
            IOptions<LocalizationOptions> localization, CancellationToken ct,
            long? categoryId = null, long? subcategoryId = null, int skip = 0) =>
            Results.Ok(await home.LoadAsync(await access.ActorAsync(http, ct),
                RequestLanguage.Resolve(http, localization.Value), categoryId, subcategoryId, skip, ct)));
        group.MapGet("/records", async (HttpContext http, AccessService access, HomeService home,
            IOptions<LocalizationOptions> localization, CancellationToken ct,
            long? categoryId = null, long? subcategoryId = null, int skip = 0, int take = 12) =>
            Results.Ok(await home.RecordsAsync(await access.ActorAsync(http, ct),
                RequestLanguage.Resolve(http, localization.Value), categoryId, subcategoryId, skip, take, ct)))
            .RequireAuthorization();
    }
}
