using handytool_api.Configuration;
using handytool_api.Models;
using handytool_api.Services;

namespace handytool_api.Tests;

public class AuthThrottleRulesTests
{
    private static readonly BruteForceOptions Options = new();

    private static TimeSpan Lockout(int failedCount) => AuthThrottleRules.LockoutFor(failedCount, Options);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void The_first_few_failures_are_not_throttled(int failedCount)
    {
        // Mistyping a password four times is ordinary. Throttling it would punish the people the
        // system exists to serve, far more often than it would inconvenience an attacker.
        Assert.Equal(TimeSpan.Zero, Lockout(failedCount));
    }

    [Fact]
    public void Throttling_starts_at_the_threshold()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), Lockout(5));
    }

    [Fact]
    public void Each_further_failure_doubles_the_wait()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), Lockout(5));
        Assert.Equal(TimeSpan.FromSeconds(120), Lockout(6));
        Assert.Equal(TimeSpan.FromSeconds(240), Lockout(7));
        Assert.Equal(TimeSpan.FromSeconds(480), Lockout(8));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(50)]
    [InlineData(5_000)]
    [InlineData(int.MaxValue)]
    public void The_wait_is_capped_however_many_failures_there_are(int failedCount)
    {
        // The cap is the whole answer to account-lockout denial of service. An attacker who fails
        // against somebody else's email deliberately, forever, still only costs them 15 minutes -
        // and cannot overflow the doubling into something absurd.
        Assert.Equal(TimeSpan.FromMinutes(Options.MaximumLockoutMinutes), Lockout(failedCount));
    }

    [Fact]
    public void Lockout_is_never_permanent()
    {
        foreach (var failedCount in Enumerable.Range(1, 200))
        {
            Assert.True(Lockout(failedCount) <= TimeSpan.FromMinutes(Options.MaximumLockoutMinutes));
        }
    }

    [Fact]
    public void Tightened_settings_are_honoured()
    {
        var strict = new BruteForceOptions
        {
            FailureThreshold = 3,
            BaseLockoutSeconds = 30,
            MaximumLockoutMinutes = 2
        };

        Assert.Equal(TimeSpan.Zero, AuthThrottleRules.LockoutFor(2, strict));
        Assert.Equal(TimeSpan.FromSeconds(30), AuthThrottleRules.LockoutFor(3, strict));
        Assert.Equal(TimeSpan.FromSeconds(60), AuthThrottleRules.LockoutFor(4, strict));
        Assert.Equal(TimeSpan.FromMinutes(2), AuthThrottleRules.LockoutFor(5, strict));
    }

    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_recent_failure_still_counts()
    {
        Assert.False(AuthThrottleRules.HasExpired(Now.AddMinutes(-5), Now, Options));
    }

    [Fact]
    public void A_failure_older_than_the_window_is_forgotten()
    {
        // Somebody who mistyped twice this morning and three times tonight is not under attack.
        Assert.True(AuthThrottleRules.HasExpired(Now.AddMinutes(-Options.AttemptWindowMinutes - 1), Now, Options));
    }
}

public class AuthThrottleEntryTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_counter_with_no_lockout_is_not_blocking()
    {
        var entry = new AuthThrottle { FailedCount = 3, LockedUntil = null };

        Assert.False(entry.IsLockedAt(Now));
    }

    [Fact]
    public void A_lockout_blocks_until_it_expires_and_then_stops()
    {
        var entry = new AuthThrottle { FailedCount = 5, LockedUntil = Now.AddMinutes(1) };

        Assert.True(entry.IsLockedAt(Now));
        Assert.False(entry.IsLockedAt(Now.AddMinutes(2)));
    }
}
