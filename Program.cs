using System.Text.Json;
using System.Text.Json.Serialization;
using handytool_api.Data;
using handytool_api.Endpoints;
using handytool_api.Logging;
using handytool_api.OpenApi;
using handytool_api.Validation;
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

    builder.Services.AddScoped<RecordValueValidator>();

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

    // Liveness ping, not part of the documented API surface.
    app.MapGet("/", () => "HandyTool API").ExcludeFromDescription();

    app.MapObjectDefinitionEndpoints();
    app.MapObjectRecordEndpoints();

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
