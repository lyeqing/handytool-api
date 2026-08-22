using handytool_api.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace handytool_api.OpenApi;

/// <summary>
/// Fills in the document-level details the built-in generator cannot infer: the API description and
/// the ownership header, exposed as an API key scheme so Swagger UI offers an "Authorize" box for it.
/// </summary>
public sealed class HandyToolDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string OwnerSchemeId = "OwnerId";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "HandyTool API",
            Version = "v1",
            Description =
                "Metadata-driven data platform. Users design their own object types " +
                "(ObjectDefinition + FieldDefinition + FieldOption); every record of every type is " +
                "stored in the single ObjectRecords table with its dynamic values in one jsonb column.\n\n" +
                $"Ownership is a placeholder until authentication exists: send a positive integer " +
                $"`{CurrentOwner.HeaderName}` header (use Authorize above, e.g. 25). It is never read " +
                "from a request body."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[OwnerSchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = CurrentOwner.HeaderName,
            Description = "Owning user/account/tenant id, for example 25."
        };

        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(OwnerSchemeId, document)] = []
            }
        ];

        return Task.CompletedTask;
    }
}
