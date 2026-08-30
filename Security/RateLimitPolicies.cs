using System.Globalization;
using System.Threading.RateLimiting;
using handytool_api.Configuration;
using handytool_api.Logging;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace handytool_api.Security;

/// <summary>
/// The API's rate-limit policies, built on the in-box middleware - no third-party library is needed
/// for any of this.
///
/// Worth being clear about the scope: this stops application abuse and crude flooding. It does not
/// stop a real distributed denial-of-service attack, because by the time a request reaches this code
/// it has already cost a connection and a thread. Anything at that scale has to be turned away at the
/// edge; see docs/tracking-and-auth.md.
/// </summary>
public static class RateLimitPolicies
{
    public const string General = "general";
    public const string Analytics = "analytics";
    public const string Expensive = "expensive";
    public const string Login = "login";

    /// <summary>Reserved for a password-reset endpoint that does not exist yet.</summary>
    public const string PasswordReset = "password-reset";

    public static IServiceCollection AddHandyToolRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(limiter =>
        {
            limiter.OnRejected = OnRejectedAsync;

            // Signed-in callers get their own bucket; everyone else shares one per address.
            AddPolicy(limiter, General, options.General, ClientAddress.RateLimitPartition);
            AddPolicy(limiter, Analytics, options.Analytics, ClientAddress.RateLimitPartition);
            AddPolicy(limiter, Expensive, options.Expensive, ClientAddress.RateLimitPartition);

            // Pre-authentication: there is no user id yet, so address is all there is.
            AddPolicy(limiter, Login, options.Login, ClientAddress.IpPartition);
            AddPolicy(limiter, PasswordReset, options.PasswordReset, ClientAddress.IpPartition);
        });

        return services;
    }

    private static void AddPolicy(
        RateLimiterOptions limiter,
        string name,
        RateLimitPolicyOptions policy,
        Func<HttpContext, string> partitionKey)
    {
        limiter.AddPolicy(name, httpContext => CreateLimiter(policy, partitionKey(httpContext)));
    }

    private static RateLimitPartition<string> CreateLimiter(RateLimitPolicyOptions policy, string key)
    {
        var window = TimeSpan.FromSeconds(Math.Max(1, policy.WindowSeconds));
        var permitLimit = Math.Max(1, policy.PermitLimit);
        var queueLimit = Math.Max(0, policy.QueueLimit);

        // A fixed window is simpler but lets a caller spend a full allowance either side of the
        // boundary. Segments smooth that out for the policies where the burst would matter.
        if (policy.SegmentsPerWindow > 1)
        {
            return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                SegmentsPerWindow = policy.SegmentsPerWindow,
                QueueLimit = queueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
        }

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = queueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    /// <summary>
    /// A rejected request must look rejected. Returning 200 with an empty body would leave a client
    /// unable to tell throttling from success, and would let a retry loop hammer away none the wiser.
    /// </summary>
    private static ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        // Tells a well-behaved client - including the future mobile apps - exactly how long to wait,
        // instead of leaving it to guess and retry into the same wall.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var policy = httpContext.GetEndpoint()?.DisplayName ?? httpContext.Request.Path.Value ?? "unknown";

        SecurityLog.RateLimited(
            SecurityLog.Create(httpContext.RequestServices),
            policy,
            ClientAddress.RateLimitPartition(httpContext),
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? string.Empty);

        return new ValueTask(httpContext.Response.WriteAsJsonAsync(
            new
            {
                title = "Too many requests.",
                status = StatusCodes.Status429TooManyRequests,
                detail = "Slow down and try again shortly."
            },
            cancellationToken));
    }
}

/// <summary>Applies a policy to a whole endpoint group, so a new endpoint cannot arrive unprotected.</summary>
public static class RateLimitEndpointExtensions
{
    public static TBuilder WithRateLimit<TBuilder>(this TBuilder builder, string policyName)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireRateLimiting(policyName);
        builder.WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status429TooManyRequests, typeof(void)));
        return builder;
    }
}
