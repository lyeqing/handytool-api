using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using handytool_api.Contracts;
using handytool_api.Models;

namespace handytool_api.Tests;

public class OpenApiSchemaTests
{
    private static JsonSerializerOptions Options() => new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Record_request_schema_can_be_exported()
    {
        // This is the exporter OpenAPI calls before applying schema transformers.
        var schema = Options().GetJsonSchemaAsNode(typeof(SaveObjectRecordRequest));
        var properties = schema["properties"]!.AsObject();
        Assert.True(properties.ContainsKey("title"));
        Assert.True(properties.ContainsKey("values"));
        Assert.True(properties.ContainsKey("description"));
        Assert.True(properties.ContainsKey("revision"));
        Assert.True(properties.ContainsKey("visibility"));
    }

    [Fact]
    public void Omitted_fields_keep_existing_defaults()
    {
        var request = JsonSerializer.Deserialize<SaveObjectRecordRequest>("{}", Options())!;
        Assert.Null(request.Title);
        Assert.Null(request.Description);
        Assert.Null(request.Revision);
        Assert.Equal(RecordVisibility.Private, request.Visibility);
        Assert.Equal(JsonValueKind.Undefined, request.Values.ValueKind);
    }

    [Fact]
    public void Explicit_null_values_stay_distinct_from_omitted_values()
    {
        var request = JsonSerializer.Deserialize<SaveObjectRecordRequest>("""{"values":null}""", Options())!;
        Assert.Equal(JsonValueKind.Null, request.Values.ValueKind);
    }

    [Fact]
    public void Record_request_fields_still_bind_from_json()
    {
        var request = JsonSerializer.Deserialize<SaveObjectRecordRequest>("""
            { "title": "Inspection", "description": "Monthly check", "values": { "score": 8 },
              "revision": 3, "visibility": "Company" }
            """, Options())!;
        Assert.Equal("Inspection", request.Title);
        Assert.Equal("Monthly check", request.Description);
        Assert.Equal(8, request.Values.GetProperty("score").GetInt32());
        Assert.Equal(3, request.Revision);
        Assert.Equal(RecordVisibility.Company, request.Visibility);
    }
}
