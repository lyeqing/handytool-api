namespace handytool_api.Models;

/// <summary>Which axis a failed-attempt counter is tracking.</summary>
public enum AuthThrottleScope
{
    /// <summary>One source address. Catches a single machine working through a password list.</summary>
    Ip = 0,

    /// <summary>
    /// One account, however many addresses the attempts come from. Catches the case IP limiting
    /// cannot: a botnet trying the same email from a thousand different places, a handful of guesses
    /// each.
    /// </summary>
    Account = 1
}

/// <summary>
/// Consecutive authentication failures for one IP or one account.
///
/// In the database rather than in memory on purpose: counters survive a restart, so an attacker
/// cannot wait one out, and they are shared if this ever runs on more than one instance.
/// </summary>
public class AuthThrottle
{
    public long Id { get; set; }

    public AuthThrottleScope Scope { get; set; }

    /// <summary>
    /// SHA-256 of the address or the lowercased email - never the value itself.
    ///
    /// The account scope has to count attempts against addresses that were never registered, since
    /// only counting real ones would make the table a way of asking which emails exist. That means
    /// this table would otherwise accumulate whatever addresses an attacker cared to submit, in the
    /// clear. Hashing also covers IP addresses, which are personal data in their own right.
    /// </summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Consecutive failures. Reset to zero by a success, or by the window elapsing.</summary>
    public int FailedCount { get; set; }

    public DateTime FirstFailedAt { get; set; }

    public DateTime LastFailedAt { get; set; }

    /// <summary>
    /// When throttling lifts. Always a bounded time in the future - never null-as-forever. A
    /// permanent lockout would hand anyone who knows an email address a way to lock its owner out.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    public bool IsLockedAt(DateTime utcNow) => LockedUntil is { } until && until > utcNow;
}
