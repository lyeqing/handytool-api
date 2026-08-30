using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace handytool_api.OpenApi;

/// <summary>
/// Fills in the document-level details the built-in generator cannot infer: the API description and
/// the bearer scheme, so Swagger UI offers an "Authorize" box to paste a session token into.
/// </summary>
public sealed class HandyToolDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string SessionSchemeId = "SessionToken";

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
                "Authentication is an opaque, database-backed session token - no JWT. Sign in at " +
                "`/api/auth/login`, then send `Authorization: Bearer <token>`. The token is a " +
                "credential, not an identity: it resolves server-side to a stable user id, is stored " +
                "only as a hash, and can be revoked at any time.\n\n" +
                "Analytics keeps three identities apart. `VisitorId` is the browser, held in the " +
                "HttpOnly `visitor_id` cookie; `SessionId` is one period of browsing; `UserId` is the " +
                "account. Signing in links the visitor to the user - it never replaces the visitor, " +
                "and never rewrites earlier anonymous events."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SessionSchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description =
                "The opaque token returned by /api/auth/login. Paste the token alone - Swagger UI " +
                "adds the \"Bearer \" prefix."
        };

        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SessionSchemeId, document)] = []
            }
        ];

        return Task.CompletedTask;
    }
}
