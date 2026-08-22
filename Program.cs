using System.Text.Json;
using System.Text.Json.Serialization;
using handytool_api.Data;
using handytool_api.Endpoints;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "HandyTool API");

app.MapObjectDefinitionEndpoints();
app.MapObjectRecordEndpoints();

app.Run();
