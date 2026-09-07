using System.Text.Json;
using System.Text.Json.Nodes;
using handytool_api.Contracts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace handytool_api.OpenApi;

/// <summary>
/// The dynamic parts of the model are <see cref="JsonElement"/>, which the generator can only
/// describe as "any JSON". This narrows them to objects and attaches realistic examples so
/// "Try it out" in Swagger UI is pre-filled with something that actually works.
/// </summary>
public sealed class HandyToolSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (type == typeof(JsonElement))
        {
            schema.Type = JsonSchemaType.Object;
            schema.AdditionalPropertiesAllowed = true;
            schema.Description =
                "A free-form JSON object. For record values the keys are field keys and the values " +
                "must match each field's type; for field settings the keys are setting names.";
        }
        else if (type == typeof(SaveObjectRecordRequest))
        {
            schema.Example = JsonNode.Parse(
                """
                {
                  "title": "August Property Inspection",
                  "description": "Routine inspection of the property.",
                  "values": {
                    "propertyAddress": "10 King William Street",
                    "inspectionDate": "2026-08-22",
                    "conditionScore": 8,
                    "damageFound": true,
                    "damageType": "water"
                  }
                }
                """);
        }
        else if (type == typeof(CreateObjectDefinitionRequest))
        {
            schema.Example = JsonNode.Parse(
                """
                {
                  "name": "Property Inspection",
                  "description": "Routine inspection of a property.",
                  "fields": [
                    {
                      "key": "propertyAddress",
                      "name": "Property Address",
                      "fieldType": "ShortText",
                      "isRequired": true,
                      "displayOrder": 1,
                      "settings": { "minimumLength": 2, "maximumLength": 200 }
                    },
                    {
                      "key": "inspectionDate",
                      "name": "Inspection Date",
                      "fieldType": "Date",
                      "isRequired": true,
                      "displayOrder": 2
                    },
                    {
                      "key": "conditionScore",
                      "name": "Condition Score",
                      "fieldType": "Range",
                      "isRequired": true,
                      "displayOrder": 3,
                      "settings": { "minimum": 1, "maximum": 10, "step": 1 }
                    },
                    {
                      "key": "damageFound",
                      "name": "Damage Found",
                      "fieldType": "Boolean",
                      "isRequired": true,
                      "displayOrder": 4
                    },
                    {
                      "key": "damageType",
                      "name": "Damage Type",
                      "fieldType": "Dropdown",
                      "displayOrder": 5,
                      "options": [
                        { "value": "water", "label": "Water Damage", "displayOrder": 1 },
                        { "value": "structural", "label": "Structural Damage", "displayOrder": 2 },
                        { "value": "cosmetic", "label": "Cosmetic Damage", "displayOrder": 3 }
                      ]
                    }
                  ]
                }
                """);
        }

        return Task.CompletedTask;
    }
}
