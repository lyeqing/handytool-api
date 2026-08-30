namespace handytool_api.Logging;

/// <summary>
/// One log category for everything security-relevant, so abuse can be watched without reading through
/// ordinary request logs.
///
/// What is deliberately never written here: passwords, raw session tokens, token hashes, and
/// password-reset tokens. A log file is copied, shipped and retained far more freely than a database,
/// and a credential in it is a credential leaked.
///
/// Email addresses are also kept out of failure paths. A log of failed sign-ins that names the
/// address attempted is a list of accounts worth attacking - the user id is enough to investigate
/// with, and only exists once the account is known to be real.
/// </summary>
public static class SecurityLog
{
    /// <summary>Sits under the "handytool_api" Serilog override, so it is on at Information by default.</summary>
    public const string Category = "handytool_api.Security";

    public static ILogger Create(ILoggerFactory loggerFactory) => loggerFactory.CreateLogger(Category);

    public static ILogger Create(IServiceProvider services) =>
        Create(services.GetRequiredService<ILoggerFactory>());

    public static void FailedSignIn(ILogger logger, string clientIp, int ipFailures, int accountFailures) =>
        logger.LogInformation(
            "Failed sign-in from {ClientIp}. Consecutive failures: {IpFailures} for this address, {AccountFailures} for the account attempted.",
            clientIp,
            ipFailures,
            accountFailures);

    public static void ThrottleApplied(ILogger logger, string scope, string clientIp, TimeSpan duration) =>
        logger.LogWarning(
            "Sign-in throttled by {Scope} after repeated failures. Request from {ClientIp} blocked for {LockoutSeconds}s.",
            scope,
            clientIp,
            (int)duration.TotalSeconds);

    /// <summary>
    /// Written on every successful sign-in, which makes it the audit trail for who got in from where.
    /// It does not claim to have cleared anything: the counters are reset unconditionally, and most
    /// of the time there was nothing there to reset.
    /// </summary>
    public static void SignedIn(ILogger logger, long userId, string clientIp) =>
        logger.LogInformation("Sign-in succeeded for user {UserId} from {ClientIp}.", userId, clientIp);

    public static void RateLimited(ILogger logger, string policy, string partition, string method, string path) =>
        logger.LogWarning(
            "Rate limit {Policy} rejected {Method} {Path} for partition {Partition}.",
            policy,
            method,
            path,
            partition);

    public static void SessionRevoked(ILogger logger, long userId, Guid sessionId, string reason) =>
        logger.LogInformation(
            "Revoked session {SessionId} for user {UserId} ({Reason}).",
            sessionId,
            userId,
            reason);
}
