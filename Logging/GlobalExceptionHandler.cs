using handytool_api.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace handytool_api.Logging;

/// <summary>
/// Logs every unhandled exception once, with the request that caused it, and converts it into a
/// ProblemDetails response so clients never receive a stack trace.
///
/// In Development it deliberately does not handle the exception after logging, so the built-in
/// developer exception page still renders while you are debugging.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled exception on {Method} {Path} for user {UserId}",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            CurrentUser.TryGetUserId(httpContext, out var userId) ? userId.ToString() : "(anonymous)");

        if (_environment.IsDevelopment())
        {
            // Logged, but left unhandled so the developer exception page can show the detail.
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "The failure has been logged. Contact support with the trace id if it persists.",
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
