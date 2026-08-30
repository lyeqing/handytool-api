using handytool_api.Configuration;
using handytool_api.Contracts;
using handytool_api.Security;
using handytool_api.Services;
using Microsoft.Extensions.Options;

namespace handytool_api.Endpoints;

/// <summary>
/// The browser-facing tracking endpoint.
///
/// In production the website and this API share the origin <c>https://handytool.org</c> - the reverse
/// proxy sends <c>/api/*</c> here and everything else to Next.js - so the browser posts to a relative
/// <c>/api/track</c>, the visitor cookie rides along as an ordinary first-party cookie, and no CORS
/// configuration exists to get wrong. Native apps will call exactly the same endpoint.
/// </summary>
public static class TrackingEndpoints
{
    /// <summary>Log category for these endpoints - static classes cannot be used as ILogger&lt;T&gt;.</summary>
    private const string LogCategory = "handytool_api.Endpoints.Tracking";

    public static void MapTrackingEndpoints(this IEndpointRouteBuilder routes)
    {
        // Its own policy: a visible tab beats twice a minute and every open tab adds to that, so
        // this has to sit far above the general limit without leaving the endpoint unprotected.
        var group = routes.MapGroup("/api/track")
            .WithTags("Tracking")
            .WithRateLimit(RateLimitPolicies.Analytics);

        group.MapPost("/", TrackAsync)
            .WithName("Track")
            .WithSummary("Record one analytics event")
            .WithDescription(
                "Anonymous callers are welcome - that is the normal case. Identity is never taken " +
                "from the body: the visitor comes from the HttpOnly `visitor_id` cookie, the browsing " +
                "session from the visitor's recent activity, and the user from the bearer token if " +
                "one was sent. `login` and `logout` are written by the auth endpoints and are " +
                "rejected here.")
            .AllowAnonymous()
            .Produces<TrackAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ValidationErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/config", GetConfig)
            .WithName("TrackingConfig")
            .WithSummary("Read the tracker settings the browser needs")
            .WithDescription("Lets the heartbeat interval be changed server-side without redeploying the website.")
            .AllowAnonymous()
            .Produces<TrackingConfigResponse>();
    }

    private static async Task<IResult> TrackAsync(
        TrackEventRequest request,
        HttpContext httpContext,
        AnalyticsTracker tracker,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var outcome = await tracker.RecordAsync(
            httpContext,
            request.EventType,
            request.Path,
            request.ActiveSeconds,
            request.Referrer ?? httpContext.Request.Headers.Referer.ToString(),
            request.Language,
            cancellationToken);

        switch (outcome.Rejection)
        {
            case TrackingRejection.None:
                // 202 with an all-but-empty body: the browser has nothing to do with the answer, and
                // there is nothing here it is allowed to know.
                return Results.Accepted(value: new TrackAcceptedResponse(true));

            case TrackingRejection.IgnoredPath:
                // Not an error. An asset or an API path simply is not website activity, and telling
                // the browser off for it would only produce console noise.
                return Results.NoContent();

            case TrackingRejection.ReservedEventType:
                loggerFactory.CreateLogger(LogCategory).LogInformation(
                    "Rejected client-sent reserved event type {EventType}", request.EventType);

                return ApiResults.ValidationFailed(
                    "That event type is recorded by the server.",
                    new Validation.RecordValidationError("eventType", "reserved_event_type",
                        "login and logout are written by the authentication endpoints."));

            default:
                return ApiResults.ValidationFailed(
                    "The event is invalid.",
                    new Validation.RecordValidationError("eventType", Validation.RecordValidationErrorCodes.Required,
                        "eventType is required."));
        }
    }

    private static IResult GetConfig(IOptions<TrackingOptions> options) =>
        Results.Ok(new TrackingConfigResponse(
            options.Value.HeartbeatSeconds,
            options.Value.MaximumActiveSecondsPerEvent));
}
