using System.Security.Cryptography;
using System.Text;
using handytool_api.Configuration;
using handytool_api.Data;
using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace handytool_api.Services;

/// <summary>The state of both counters for one sign-in attempt.</summary>
public sealed record ThrottleDecision(
    bool IsThrottled,
    TimeSpan RetryAfter,
    AuthThrottleScope? Scope,
    int IpFailures,
    int AccountFailures)
{
    public static readonly ThrottleDecision Allowed = new(false, TimeSpan.Zero, null, 0, 0);
}

/// <summary>
/// Counts consecutive sign-in failures on two axes at once, and throttles on whichever trips first.
///
/// Neither axis is sufficient alone. IP-only protection fails against a botnet, which simply spreads
/// its guesses; account-only protection punishes everyone behind a shared office address for one
/// person's bad afternoon. Together, the common attacks are covered without the common false
/// positives.
/// </summary>
public sealed class AuthThrottleService
{
    private readonly HandyToolDbContext _db;
    private readonly BruteForceOptions _options;

    public AuthThrottleService(HandyToolDbContext db, IOptions<BruteForceOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <summary>
    /// Asked before a password is checked. Being throttled has to cost nothing to evaluate - if the
    /// block itself did the expensive work, it would be the attack.
    /// </summary>
    public async Task<ThrottleDecision> CheckAsync(string? email, string clientIp, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var ipEntry = await FindAsync(AuthThrottleScope.Ip, clientIp, cancellationToken);
        var accountEntry = _options.AccountScopeEnabled
            ? await FindAsync(AuthThrottleScope.Account, NormaliseEmail(email), cancellationToken)
            : null;

        var ipFailures = Live(ipEntry, now);
        var accountFailures = Live(accountEntry, now);

        // IP first: when one machine is working through a list, that is the axis that identifies it,
        // and saying so keeps the account counter out of the log line.
        if (ipEntry is not null && ipEntry.IsLockedAt(now))
        {
            return new ThrottleDecision(true, ipEntry.LockedUntil!.Value - now, AuthThrottleScope.Ip, ipFailures, accountFailures);
        }

        if (accountEntry is not null && accountEntry.IsLockedAt(now))
        {
            return new ThrottleDecision(true, accountEntry.LockedUntil!.Value - now, AuthThrottleScope.Account, ipFailures, accountFailures);
        }

        return ThrottleDecision.Allowed with { IpFailures = ipFailures, AccountFailures = accountFailures };
    }

    /// <summary>
    /// Records one wrong password against both axes.
    ///
    /// Called for an unknown email as well as a known one. Only counting real accounts would turn the
    /// throttle into an oracle: "this address never locks out" answers the question the generic error
    /// message exists to avoid.
    /// </summary>
    public async Task<ThrottleDecision> RecordFailureAsync(
        string? email,
        string clientIp,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var ip = await IncrementAsync(AuthThrottleScope.Ip, clientIp, now, cancellationToken);

        var account = _options.AccountScopeEnabled
            ? await IncrementAsync(AuthThrottleScope.Account, NormaliseEmail(email), now, cancellationToken)
            : null;

        await _db.SaveChangesAsync(cancellationToken);

        var scope = ip.IsLockedAt(now)
            ? AuthThrottleScope.Ip
            : account is not null && account.IsLockedAt(now) ? AuthThrottleScope.Account : (AuthThrottleScope?)null;

        var retryAfter = scope switch
        {
            AuthThrottleScope.Ip => ip.LockedUntil!.Value - now,
            AuthThrottleScope.Account => account!.LockedUntil!.Value - now,
            _ => TimeSpan.Zero
        };

        return new ThrottleDecision(
            scope is not null,
            retryAfter,
            scope,
            ip.FailedCount,
            account?.FailedCount ?? 0);
    }

    /// <summary>
    /// Clears both counters after a correct password.
    ///
    /// This is what keeps an account-targeted lockout survivable: whatever an attacker has built up
    /// against an address, the owner signing in once wipes it.
    /// </summary>
    public async Task ClearAsync(string? email, string clientIp, CancellationToken cancellationToken)
    {
        var keys = new List<(AuthThrottleScope Scope, string Hash)>
        {
            (AuthThrottleScope.Ip, HashKey(clientIp))
        };

        if (_options.AccountScopeEnabled)
        {
            keys.Add((AuthThrottleScope.Account, HashKey(NormaliseEmail(email))));
        }

        var scopes = keys.Select(k => k.Scope).ToList();
        var hashes = keys.Select(k => k.Hash).ToList();

        var entries = await _db.AuthThrottles
            .Where(t => scopes.Contains(t.Scope) && hashes.Contains(t.KeyHash))
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return;
        }

        _db.AuthThrottles.RemoveRange(entries);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private Task<AuthThrottle?> FindAsync(AuthThrottleScope scope, string key, CancellationToken cancellationToken)
    {
        var keyHash = HashKey(key);

        return _db.AuthThrottles
            .FirstOrDefaultAsync(t => t.Scope == scope && t.KeyHash == keyHash, cancellationToken);
    }

    private async Task<AuthThrottle> IncrementAsync(
        AuthThrottleScope scope,
        string key,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var entry = await FindAsync(scope, key, cancellationToken);

        if (entry is null)
        {
            entry = new AuthThrottle
            {
                Scope = scope,
                KeyHash = HashKey(key),
                FailedCount = 0,
                FirstFailedAt = now,
                LastFailedAt = now
            };

            _db.AuthThrottles.Add(entry);
        }
        else if (AuthThrottleRules.HasExpired(entry.LastFailedAt, now, _options))
        {
            // Cold counter: this is a new run of attempts, not a continuation of one from hours ago.
            entry.FailedCount = 0;
            entry.FirstFailedAt = now;
            entry.LockedUntil = null;
        }

        entry.FailedCount++;
        entry.LastFailedAt = now;

        var lockout = AuthThrottleRules.LockoutFor(entry.FailedCount, _options);
        entry.LockedUntil = lockout > TimeSpan.Zero ? now.Add(lockout) : null;

        return entry;
    }

    /// <summary>Live failure count, ignoring a counter that has gone cold.</summary>
    private int Live(AuthThrottle? entry, DateTime now) =>
        entry is null || AuthThrottleRules.HasExpired(entry.LastFailedAt, now, _options) ? 0 : entry.FailedCount;

    private static string NormaliseEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// SHA-256, unsalted so the same key always lands on the same row. This is a lookup key, not a
    /// password: there is no secret to protect, only a value we would rather not store in the clear.
    /// </summary>
    private static string HashKey(string key) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
