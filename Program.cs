using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using handytool_api.Configuration;
using handytool_api.Data;
using handytool_api.Endpoints;
using handytool_api.Logging;
using handytool_api.OpenApi;
using handytool_api.Security;
using handytool_api.Services;
using handytool_api.Validation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

// Requests slower than this are logged as a warning even when they succeed.
const int SlowRequestMilliseconds = 2000;

// Bootstrap logger: replaced below by the configured one, but it means anything that blows up during
// startup (bad connection string, missing config) is logged instead of vanishing.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Levels and sinks live in appsettings.json under "Serilog" so they can be changed per
    // environment without a rebuild.
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<HandyToolDocumentTransformer>();
        options.AddSchemaTransformer<HandyToolSchemaTransformer>();
    });

    builder.Services.AddDbContext<HandyToolDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("HandyTool")));

    builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
    builder.Services.Configure<BruteForceOptions>(builder.Configuration.GetSection(BruteForceOptions.SectionName));
    builder.Services.Configure<TrackingOptions>(builder.Configuration.GetSection(TrackingOptions.SectionName));
    builder.Services.Configure<LocalizationOptions>(builder.Configuration.GetSection(LocalizationOptions.SectionName));

    // Which proxies may tell us the client address. Nothing is trusted unless it is configured, so a
    // missing section degrades to the socket address rather than to a spoofable header.
    var forwardedHeaders = builder.Configuration
        .GetSection(ForwardedHeaderOptions.SectionName)
        .Get<ForwardedHeaderOptions>() ?? new ForwardedHeaderOptions();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = forwardedHeaders.ForwardLimit;

        // The defaults trust loopback. Cleared so the trusted set is exactly what configuration says
        // and nothing else.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var proxy in forwardedHeaders.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        foreach (var network in forwardedHeaders.KnownNetworks)
        {
            // A malformed entry is skipped rather than fatal, but it is not silent either: it would
            // otherwise widen or narrow what we trust without anyone noticing.
            // Fully qualified: Microsoft.AspNetCore.HttpOverrides has a type of the same name.
            if (System.Net.IPNetwork.TryParse(network, out var parsed))
            {
                options.KnownIPNetworks.Add(parsed);
            }
            else
            {
                Log.Warning("Ignoring malformed trusted network {Network} in configuration.", network);
            }
        }
    });

    // Application-level abuse protection. Not a substitute for edge DDoS protection - see
    // docs/tracking-and-auth.md.
    var rateLimiting = builder.Configuration
        .GetSection(RateLimitingOptions.SectionName)
        .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

    builder.Services.AddHandyToolRateLimiting(builder.Configuration);

    // Opaque database-backed session tokens, not JWT: the token carries nothing, so revoking a
    // session takes effect on the very next request rather than whenever a signature would expire.
    builder.Services
        .AddAuthentication(SessionTokenAuthenticationHandler.SchemeName)
        .AddScheme<SessionTokenAuthenticationOptions, SessionTokenAuthenticationHandler>(
            SessionTokenAuthenticationHandler.SchemeName,
            _ => { });

    builder.Services.AddAuthorization();

    builder.Services.AddScoped<RecordValueValidator>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<AuthThrottleService>();
    builder.Services.AddScoped<TrackingIdentityResolver>();
    builder.Services.AddScoped<AnalyticsTracker>();

    // Access control, plan quotas and the anonymous trial. AccessService, DefinitionLoader and
    // TrialQuota take the DbContext, so they are scoped with it; TrialIdentity holds only a data
    // protector and ApiFailureFilter holds no state at all.
    builder.Services.AddScoped<AccessService>();
    builder.Services.AddScoped<HomeService>();
    builder.Services.AddScoped<DefinitionLoader>();
    builder.Services.AddScoped<TrialQuota>();
    builder.Services.AddSingleton<TrialIdentity>();
    builder.Services.AddSingleton<ApiFailureFilter>();

    // Unhandled exceptions get logged once, with context, and turned into ProblemDetails.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Mobile clients (Android/iOS) get camelCase JSON and string enums.
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    var app = builder.Build();

    if (app.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
    {
        if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo seeding requires Development.");
        await using var scope = app.Services.CreateAsyncScope();
        await DevelopmentSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<HandyToolDbContext>(), app.Configuration);
        return;
    }

    // First in the pipeline: everything downstream that cares who is calling - rate limiting, the
    // security log, the brute-force counters - reads Connection.RemoteIpAddress, and it has to be the
    // real client by then rather than the reverse proxy.
    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment() && !forwardedHeaders.HasTrustedProxies)
    {
        // Loud on purpose. Silently rate-limiting every visitor as though they were one caller is the
        // kind of failure that only shows up as "the site is broken for everyone at once".
        Log.Warning(
            "No trusted proxies are configured under \"{Section}\". X-Forwarded-For will be ignored, so " +
            "every request behind the reverse proxy will appear to come from the proxy itself and will " +
            "share one rate-limit bucket.",
            ForwardedHeaderOptions.SectionName);
    }

    app.UseExceptionHandler();

    // One line per request, but only when it is worth reading. 4xx is deliberately silent here:
    // the endpoints already log their own rejection reason, which says far more than a status code.
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, elapsedMilliseconds, exception) =>
            exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? LogEventLevel.Error
                : elapsedMilliseconds > SlowRequestMilliseconds
                    ? LogEventLevel.Warning
                    : LogEventLevel.Debug;
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        // Document at /openapi/v1.json, Swagger UI over the top of it at /swagger.
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "HandyTool API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "HandyTool API";
        });
    }

    // Not in development: the Next.js server and the mobile simulators talk to the plain http endpoint,
    // and Node's fetch does not trust the ASP.NET Core dev certificate.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // No CORS anywhere, by design. In production a reverse proxy serves the website at
    // https://handytool.org/ and this API at https://handytool.org/api/*, so every browser call is
    // same-origin; in development the Next.js dev server rewrites /api/* here, which the browser also
    // sees as same-origin. That is what lets the visitor cookie stay a plain first-party SameSite=Lax
    // cookie instead of needing SameSite=None.
    app.UseAuthentication();
    app.UseAuthorization();

    // After authentication on purpose: a signed-in caller is rate-limited on their stable user id
    // rather than sharing a bucket with everyone behind the same office address. The cost is that an
    // unauthenticated flood still pays for one indexed token lookup before being turned away - cheap,
    // and the general policy still stops it.
    if (rateLimiting.Enabled)
    {
        app.UseRateLimiter();
    }
    else
    {
        // A local debugging convenience that must never be shipped. Saying so at startup is the only
        // way anybody would find out.
        Log.Warning("Rate limiting is DISABLED by configuration. Do not run production this way.");
    }

    // Liveness ping, not part of the documented API surface.
    app.MapGet("/", () => "HandyTool API").ExcludeFromDescription();

    app.MapAuthEndpoints();
    app.MapTrackingEndpoints();
    app.MapAnalyticsEndpoints();
    app.MapObjectDefinitionEndpoints();
    app.MapObjectRecordEndpoints();
    app.MapCategoryEndpoints();

    app.Run();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "HandyTool API terminated unexpectedly during startup.");
    throw;
}
finally
{
    // Flush anything still buffered in the file sink before the process exits.
    Log.CloseAndFlush();
}
