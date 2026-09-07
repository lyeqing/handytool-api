using System.Reflection;
using handytool_api.Contracts;
using handytool_api.Endpoints;
using handytool_api.Models;
using handytool_api.Services;
using handytool_api.Validation;

namespace handytool_api.Tests;

public class DefinitionShapeTests
{
    // Exercise the endpoint's request validation without exposing it as a public API.
    private static readonly Func<CreateObjectDefinitionRequest, List<RecordValidationError>> Validate =
        typeof(ObjectDefinitionEndpoints).GetMethod("ValidateShape", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<CreateObjectDefinitionRequest, List<RecordValidationError>>>();

    [Theory]
    [InlineData(512, true)]
    [InlineData(513, false)]
    public void Top_level_fields_respect_loader_limit(int count, bool valid)
    {
        var request = new CreateObjectDefinitionRequest("Example", Fields:
            Enumerable.Range(0, count).Select(i => Text($"field{i}")).ToList());
        Check(request, valid);
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Collection_items_count_toward_total(int count, bool valid)
    {
        var request = new CreateObjectDefinitionRequest("Example", Fields:
            Enumerable.Range(0, count).Select(i => new CreateFieldDefinitionRequest(
                $"field{i}", "Collection", FieldType.Collection, Item: Text("item"))).ToList());
        Check(request, valid);
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public void Collection_depth_matches_loader(int collections, bool valid)
    {
        var field = Text("item");
        for (var i = 0; i < collections; i++)
            field = new CreateFieldDefinitionRequest("items", "Items", FieldType.Collection, Item: field);
        Check(new CreateObjectDefinitionRequest("Example", Fields: [field]), valid);
    }

    private static CreateFieldDefinitionRequest Text(string key) => new(key, "Text", FieldType.ShortText);

    private static void Check(CreateObjectDefinitionRequest request, bool valid)
    {
        if (valid)
        {
            Assert.Empty(Validate(request));
            return;
        }
        var failure = Assert.Throws<ApiFailure>(() => Validate(request));
        Assert.Equal(400, failure.Status);
        Assert.Equal("schema_too_complex", failure.Code);
    }
}
