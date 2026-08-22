using System.Text.Json;
using System.Text.Json.Serialization;
using handytool_api.Data;
using handytool_api.Endpoints;
using handytool_api.OpenApi;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<HandyToolDocumentTransformer>();
    options.AddSchemaTransformer<HandyToolSchemaTransformer>();
});

builder.Services.AddDbContext<HandyToolDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HandyTool")));

builder.Services.AddScoped<RecordValueValidator>();

// Mobile clients (Android/iOS) get camelCase JSON and string enums.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

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
