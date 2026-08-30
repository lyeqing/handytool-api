using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Security;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Endpoints;

/// <summary>
/// Reading the history back.
///
/// The interesting question is "show me everything associated with this user", and the answer has two
/// halves: events recorded against the account, and events recorded against a browser that we later
/// learned was theirs. The second half is why anonymous events are never rewritten - the
/// <see cref="TrackingClientUser"/> link finds them without disturbing them.
/// </summary>
public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder routes)
    {
        // Reading a full history is an unbounded scan over the events table, which makes these the
        // most expensive endpoints in the API and the cheapest to abuse.
        var group = routes.MapGroup("/api/analytics")
            .WithTags("Analytics")
            .RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.Expensive);

        group.MapGet("/users/{userId:long}/activity", GetUserActivityAsync)
            .WithName("GetUserActivity")
            .WithSummary("Read one account's full browsing history")
            .WithDescription(
                "Chronological. Includes the anonymous events from before sign-in, which keep their " +
                "null userId exactly as recorded - they are found through the visitor-to-user link, " +
                "not by rewriting them. `take` is clamped to 1-500.")
            .Produces<UserActivityResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/users/{userId:long}/page-time", GetPageActiveTimeAsync)
            .WithName("GetUserPageActiveTime")
            .WithSummary("Active seconds per page for one account")
            .WithDescription(
                "Sums the active time the browser measured while each page was actually visible. " +
                "Heartbeats make this hold up even when the final page_leave never arrives.")
            .Produces<List<PageActiveTimeResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> GetUserActivityAsync(
        long userId,
        HttpContext httpContext,
        HandyToolDbContext db,
        CancellationToken cancellationToken,
        int skip = 0,
        int take = 200)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var callerId))
        {
            return Results.Unauthorized();
        }

        // Until there are roles, an account may read its own history and nobody else's.
        if (callerId != userId)
        {
            return Results.Forbid();
        }

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 500);

        var links = await db.TrackingClientUsers
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .Join(db.TrackingClients, l => l.ClientId, c => c.Id, (l, c) => new { l, c })
            .OrderByDescending(x => x.l.LastIdentifiedAt)
            .Select(x => new LinkedClientResponse(x.l.ClientId, x.c.ClientType, x.l.FirstIdentifiedAt, x.l.LastIdentifiedAt))
            .ToListAsync(cancellationToken);

        var clientIds = links.Select(l => l.ClientId).ToList();

        // Both halves in one pass: filed against the account, or against one of the account's clients
        // back when nobody knew whose it was.
        var query = db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId || clientIds.Contains(e.ClientId));

        var total = await query.LongCountAsync(cancellationToken);

        var events = await query
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Results.Ok(new UserActivityResponse(
            userId,
            links,
            events.Select(ActivityEventResponse.From).ToList(),
            skip,
            take,
            total));
    }

    private static async Task<IResult> GetPageActiveTimeAsync(
        long userId,
        HttpContext httpContext,
        HandyToolDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var callerId))
        {
            return Results.Unauthorized();
        }

        if (callerId != userId)
        {
            return Results.Forbid();
        }

        var clientIds = await db.TrackingClientUsers
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => l.ClientId)
            .ToListAsync(cancellationToken);

        var pages = await db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId || clientIds.Contains(e.ClientId))
            .GroupBy(e => e.Path)
            .Select(g => new PageActiveTimeResponse(
                g.Key,
                g.Sum(e => e.ActiveSeconds) ?? 0,
                g.Count(e => e.EventType == AnalyticsEventTypes.PageView)))
            .OrderByDescending(p => p.ActiveSeconds)
            .ToListAsync(cancellationToken);

        return Results.Ok(pages);
    }
}
