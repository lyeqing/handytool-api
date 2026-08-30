namespace handytool_api.Configuration;

/// <summary>
/// Bound from "Auth:BruteForce".
///
/// This lives under Auth rather than RateLimiting because it is not a middleware policy: it counts
/// authentication outcomes, not requests, and only a wrong password moves it. Somebody signing in
/// correctly ten times in a row is never affected by any of it.
/// </summary>
public sealed class BruteForceOptions
{
    public const string SectionName = "Auth:BruteForce";

    /// <summary>
    /// Failures allowed before throttling starts. Generous on purpose: mistyping a password four
    /// times is ordinary human behaviour and must not cost anybody a lockout.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>The first lockout, doubling with each further failure.</summary>
    public int BaseLockoutSeconds { get; set; } = 60;

    /// <summary>
    /// The ceiling on doubling, and the most important number here.
    ///
    /// An attacker can deliberately fail against somebody else's email and throttle that account.
    /// That cannot be prevented outright without deciding whose attempts are genuine, which is not
    /// knowable. So it is bounded instead: the worst anyone can inflict is this many minutes, and one
    /// successful sign-in clears it immediately.
    /// </summary>
    public int MaximumLockoutMinutes { get; set; } = 15;

    /// <summary>
    /// How long a counter remembers. Failures older than this are treated as unrelated, so someone
    /// who mistypes twice in March and three times in June is never treated as an attack.
    /// </summary>
    public int AttemptWindowMinutes { get; set; } = 60;

    /// <summary>
    /// Throttle the account axis at all. Present so it can be turned off if account-targeted lockout
    /// ever proves more trouble than it prevents - IP throttling keeps working either way.
    /// </summary>
    public bool AccountScopeEnabled { get; set; } = true;
}
