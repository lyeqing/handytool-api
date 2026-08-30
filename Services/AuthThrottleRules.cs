using handytool_api.Configuration;

namespace handytool_api.Services;

/// <summary>
/// The backoff schedule, as a pure function of how many times in a row someone has failed.
///
/// Separated from the database work so the escalation - and, more importantly, its ceiling - can be
/// read and tested on its own.
/// </summary>
public static class AuthThrottleRules
{
    /// <summary>
    /// How long to lock out after <paramref name="failedCount"/> consecutive failures, or
    /// <see cref="TimeSpan.Zero"/> for the early failures that pass through untouched.
    ///
    /// With the defaults - threshold 5, base 60s, cap 15 minutes:
    ///
    /// <code>
    /// failures 1-4  none
    /// failure  5    1 minute
    /// failure  6    2 minutes
    /// failure  7    4 minutes
    /// failure  8    8 minutes
    /// failure  9+   15 minutes (capped)
    /// </code>
    ///
    /// Doubling is what makes this worth having: an attacker gets a handful of guesses an hour, while
    /// somebody who genuinely forgot their password waits a minute and tries again.
    /// </summary>
    public static TimeSpan LockoutFor(int failedCount, BruteForceOptions options)
    {
        var threshold = Math.Max(1, options.FailureThreshold);

        if (failedCount < threshold)
        {
            return TimeSpan.Zero;
        }

        var maximum = TimeSpan.FromMinutes(Math.Max(1, options.MaximumLockoutMinutes));
        var baseSeconds = Math.Max(1, options.BaseLockoutSeconds);

        // Doublings past this overflow a long before they matter - everything here is capped anyway.
        var steps = Math.Min(failedCount - threshold, 32);

        var seconds = baseSeconds * Math.Pow(2, steps);

        return seconds >= maximum.TotalSeconds ? maximum : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Whether a counter has gone cold. Old failures are dropped rather than accumulated, so counts
    /// only ever describe one run of attempts.
    /// </summary>
    public static bool HasExpired(DateTime lastFailedAt, DateTime utcNow, BruteForceOptions options) =>
        lastFailedAt < utcNow.AddMinutes(-Math.Max(1, options.AttemptWindowMinutes));
}
