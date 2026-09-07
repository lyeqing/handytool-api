using System.Globalization;
using handytool_api.Configuration;
using handytool_api.Contracts;
using handytool_api.Models;
using handytool_api.Security;
using handytool_api.Services;
using Microsoft.Extensions.Options;

namespace handytool_api.Endpoints;

/// <summary>
/// Accounts and devices.
///
/// The token these endpoints hand out is opaque: random bytes with no structure and nothing encoded
/// inside. It is a credential, and the API is the only thing that can say what it means - which is
/// what makes revocation instant, and what makes it unusable as an analytics identifier.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Log category for these endpoints - static classes cannot be used as ILogger&lt;T&gt;.</summary>
    private const string LogCategory = "handytool_api.Endpoints.Auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // Deliberately no group-level rate limit. The two pre-authentication endpoints need the far
        // stricter login policy, and letting that depend on an endpoint convention out-ranking a group
        // convention would make the most important limit in the API a matter of metadata ordering.
        // Each endpoint names its own policy instead.
        var group = routes.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Create an account and sign in on this device")
            .WithRateLimit(RateLimitPolicies.Login)
            .Produces<AuthenticatedResponse>(StatusCodes.Status201Created)
            .Produces<ValidationErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ValidationErrorResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Sign in and receive an opaque session token")
            .WithDescription(
                "Send the token back as `Authorization: Bearer <token>`. Signing in on one device " +
                "never disturbs another - each device holds its own session, and all of them resolve " +
                "to the same stable user id. " +
                "Rate limited per IP, and separately throttled by consecutive failures against both " +
                "the calling address and the account being attempted. Repeated failures return 429 " +
                "with Retry-After; the lockout is always temporary and a correct password clears it.")
            .WithRateLimit(RateLimitPolicies.Login)
            .Produces<AuthenticatedResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Revoke the calling device's session")
            .RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.General)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout-all", LogoutAllAsync)
            .WithName("LogoutAll")
            .WithSummary("Revoke every other session for this account")
            .WithDescription("The calling device stays signed in. Use this after a suspected password leak.")
            .RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.General)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", MeAsync)
            .WithName("Me")
            .WithSummary("Read the signed-in account")
            .RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.General)
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/me", UpdateProfileAsync)
            .WithName("UpdateProfile")
            .WithSummary("Update the signed-in account")
            .WithDescription(
                "Partial update - omitted properties are left alone. Send an empty preferredLanguage " +
                "to go back to following the browser.")
            .RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.General)
            .Produces<UserResponse>()
            .Produces<ValidationErrorResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/sessions", ListSessionsAsync)
            .WithName("ListSessions")
            .WithSummary("List this account's active devices")
            .WithDescription("Tokens are never included - only the hash of each one is stored, and that is not shown either.")
            .RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.General)
            .Produces<List<UserSessionResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/sessions/{id:guid}", RevokeSessionAsync)
            .WithName("RevokeSession")
            .WithSummary("Sign one device out")
            .RequireAuthorization()
            .WithRateLimit(RateLimitPolicies.General)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext httpContext,
        AuthService authService,
        AnalyticsTracker tracker,
        IWebHostEnvironment environment,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);

        var outcome = await authService.RegisterAsync(
            request,
            httpContext.Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (!outcome.Succeeded)
        {
            logger.LogInformation("Rejected registration: {Failure}", outcome.Failure);
            if (outcome.Errors is { Count: > 0 })
                return ApiResults.ValidationFailed("Please check your registration details.", outcome.Errors.ToArray());
            return Rejected(outcome, httpContext);
        }

        IssueSessionCookie(httpContext, outcome, environment);

        await RecordLoginAsync(tracker, httpContext, outcome.User!.Id, cancellationToken);

        logger.LogInformation("Registered account {UserId}", outcome.User.Id);

        return Results.Created("/api/auth/me", ToResponse(outcome));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        AuthService authService,
        AnalyticsTracker tracker,
        IWebHostEnvironment environment,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);

        var outcome = await authService.LoginAsync(
            request.Email,
            request.Password,
            request.ClientType,
            request.DeviceName,
            httpContext.Request.Headers.UserAgent.ToString(),
            ClientAddress.RemoteIpAddress(httpContext),
            cancellationToken);

        if (!outcome.Succeeded)
        {
            // AuthService has already written the security-log entry, with no email address in it.
            return Rejected(outcome, httpContext);
        }

        IssueSessionCookie(httpContext, outcome, environment);

        await RecordLoginAsync(tracker, httpContext, outcome.User!.Id, cancellationToken);

        logger.LogInformation(
            "Signed in user {UserId} on session {SessionId} ({ClientType})",
            outcome.User.Id,
            outcome.Session!.Id,
            outcome.Session.ClientType);

        return Results.Ok(ToResponse(outcome));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        AuthService authService,
        AnalyticsTracker tracker,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var userId)
            || !CurrentUser.TryGetSessionId(httpContext, out var sessionId))
        {
            return Results.Unauthorized();
        }

        // Recorded before the revoke, while the principal still resolves to a user.
        await tracker.RecordServerEventAsync(httpContext, AnalyticsEventTypes.Logout, userId, cancellationToken);

        await authService.RevokeSessionAsync(userId, sessionId, cancellationToken);

        // The row is revoked either way; clearing the cookie just saves the browser from sending a
        // credential that is already dead.
        SessionCookie.Delete(httpContext, secure: !environment.IsDevelopment());

        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        HttpContext httpContext,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        CurrentUser.TryGetSessionId(httpContext, out var sessionId);

        await authService.RevokeAllSessionsAsync(userId, sessionId, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        HttpContext httpContext,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await authService.FindUserAsync(userId, cancellationToken);

        return user is null ? Results.Unauthorized() : Results.Ok(UserResponse.From(user));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        HttpContext httpContext,
        AuthService authService,
        IOptions<LocalizationOptions> localization,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        string? preferredLanguage = null;

        if (request.PreferredLanguage is { } requested)
        {
            // Empty means "stop remembering a choice". Anything else has to be a language we can
            // actually serve, or it would sit in the account row silently doing nothing.
            if (requested.Length > 0)
            {
                preferredLanguage = localization.Value.Normalise(requested);

                if (preferredLanguage is null)
                {
                    return ApiResults.ValidationFailed(
                        "That language is not supported.",
                        new Validation.RecordValidationError(
                            "preferredLanguage",
                            Validation.RecordValidationErrorCodes.UnknownOption,
                            $"Supported languages are {string.Join(", ", localization.Value.SupportedLanguages)}."));
                }
            }
        }

        var user = await authService.UpdateProfileAsync(
            userId,
            request.DisplayName,
            clearPreferredLanguage: request.PreferredLanguage is { Length: 0 },
            preferredLanguage,
            cancellationToken);

        return user is null ? Results.Unauthorized() : Results.Ok(UserResponse.From(user));
    }

    private static async Task<IResult> ListSessionsAsync(
        HttpContext httpContext,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        CurrentUser.TryGetSessionId(httpContext, out var currentSessionId);

        var sessions = await authService.ListActiveSessionsAsync(userId, cancellationToken);

        return Results.Ok(sessions.Select(s => UserSessionResponse.From(s, currentSessionId)).ToList());
    }

    private static async Task<IResult> RevokeSessionAsync(
        Guid id,
        HttpContext httpContext,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        // Scoped to the caller's own sessions, so an id from another account reads as "not found".
        var revoked = await authService.RevokeSessionAsync(userId, id, cancellationToken);

        return revoked ? Results.NoContent() : Results.NotFound();
    }

    /// <summary>
    /// Writes the <c>login</c> event and, with it, the visitor-to-user link. This is the moment an
    /// anonymous browser becomes a known one - without touching a single historical event.
    /// </summary>
    private static Task RecordLoginAsync(
        AnalyticsTracker tracker,
        HttpContext httpContext,
        long userId,
        CancellationToken cancellationToken) =>
        tracker.RecordServerEventAsync(httpContext, AnalyticsEventTypes.Login, userId, cancellationToken);

    /// <summary>
    /// Hands the browser its copy of the token as an HttpOnly cookie. Native clients ignore the
    /// cookie and use the token from the response body as a bearer credential - same token, same
    /// session row, different transport.
    /// </summary>
    private static void IssueSessionCookie(HttpContext httpContext, AuthOutcome outcome, IWebHostEnvironment environment) =>
        SessionCookie.Write(
            httpContext,
            outcome.Token!,
            outcome.Session!.ExpiresDate,
            secure: !environment.IsDevelopment());

    private static AuthenticatedResponse ToResponse(AuthOutcome outcome) => new(
        outcome.Token!,
        outcome.Session!.ExpiresDate,
        UserResponse.From(outcome.User!));

    private static IResult Rejected(AuthOutcome outcome, HttpContext httpContext)
    {
        if (outcome.Failure == AuthFailure.TooManyAttempts)
        {
            // Same shape as a middleware rate-limit rejection, so a client - including the future
            // mobile apps - only has to handle one back-off case.
            var seconds = Math.Max(1, (int)Math.Ceiling(outcome.RetryAfter.TotalSeconds));
            httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

            return Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many sign-in attempts.",
                detail: $"Try again in about {seconds} seconds.");
        }

        return RejectedCore(outcome.Failure);
    }

    private static IResult RejectedCore(AuthFailure failure) => failure switch
    {
        AuthFailure.EmailAlreadyRegistered => Results.Conflict(new ValidationErrorResponse(
            "That email address is already registered.",
            [new Validation.RecordValidationError("email", "duplicate_email", "An account with that email already exists.")])),

        AuthFailure.WeakPassword => ApiResults.ValidationFailed(
            "The password is too short.",
            new Validation.RecordValidationError("password", Validation.RecordValidationErrorCodes.TooShort, "Choose a longer password.")),

        AuthFailure.InvalidEmail => ApiResults.ValidationFailed(
            "The email address is not usable.",
            new Validation.RecordValidationError("email", Validation.RecordValidationErrorCodes.InvalidFormat, "Enter a valid email address.")),

        // One answer for every credential failure. Distinguishing "no such account" from "wrong
        // password" tells an attacker which emails are worth trying.
        _ => Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "The email address or password is incorrect.")
    };
}
