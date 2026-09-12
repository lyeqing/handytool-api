using handytool_api.Configuration;
using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Logging;
using handytool_api.Models;
using handytool_api.Security;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace handytool_api.Services;

/// <summary>Why an authentication attempt did not succeed. Deliberately coarse - see <see cref="AuthService"/>.</summary>
public enum AuthFailure
{
    None = 0,
    InvalidCredentials,
    EmailAlreadyRegistered,
    WeakPassword,
    InvalidEmail,
    InvalidRegistration,

    /// <summary>Too many recent failures on this address or this account. Temporary, always.</summary>
    TooManyAttempts
}

/// <summary>
/// The outcome of signing in or registering. <see cref="Token"/> holds the raw session token and is
/// the only copy that will ever exist outside the client.
/// </summary>
public sealed record AuthOutcome(
    AuthFailure Failure,
    UserAccount? User = null,
    UserSession? Session = null,
    string? Token = null,
    // How long until the caller may try again. Only meaningful for TooManyAttempts.
    TimeSpan RetryAfter = default,
    IReadOnlyList<RecordValidationError>? Errors = null)
{
    public bool Succeeded => Failure == AuthFailure.None;

    public static AuthOutcome Fail(AuthFailure failure) => new(failure);

    public static AuthOutcome Throttled(TimeSpan retryAfter) =>
        new(AuthFailure.TooManyAttempts, RetryAfter: retryAfter);
}

/// <summary>
/// Accounts and signed-in devices.
///
/// Sessions are opaque, database-backed and independent of one another: signing in on a phone does
/// not disturb a laptop, and every one of them resolves to the same stable
/// <see cref="UserAccount.Id"/>. No JWT is involved anywhere - the token carries no claims, so
/// revocation takes effect at once rather than whenever the token would have expired.
/// </summary>
public sealed class AuthService
{
    private readonly HandyToolDbContext _db;
    private readonly AuthOptions _options;
    private readonly AuthThrottleService _throttle;
    private readonly ILogger _securityLog;

    /// <summary>
    /// Verified against when no account matches, so a request for an unknown email costs the same
    /// time as a wrong password and cannot be used to enumerate who has an account.
    /// </summary>
    private static readonly (string Hash, string Salt) DummyCredential = PasswordHasher.Hash("no-such-account");

    public AuthService(
        HandyToolDbContext db,
        IOptions<AuthOptions> options,
        AuthThrottleService throttle,
        ILoggerFactory loggerFactory)
    {
        _db = db;
        _options = options.Value;
        _throttle = throttle;
        _securityLog = SecurityLog.Create(loggerFactory);
    }

    public async Task<AuthOutcome> RegisterAsync(
        RegisterRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request, _options.MinimumPasswordLength);
        if (errors.Count > 0) return new(AuthFailure.InvalidRegistration, Errors: errors);

        var normalisedEmail = NormaliseEmail(request.Email);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize registrations for the same normalized email before checking uniqueness.
        await new AccessService(_db).LockKeyAsync($"register:{normalisedEmail}", cancellationToken);

        if (await _db.UserAccounts.AnyAsync(u => u.Email == normalisedEmail, cancellationToken))
        {
            return AuthOutcome.Fail(AuthFailure.EmailAlreadyRegistered);
        }

        var now = DateTime.UtcNow;
        var (hash, salt) = PasswordHasher.Hash(request.Password);

        var user = new UserAccount
        {
            Email = normalisedEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            DisplayName = request.DisplayName!.Trim(),
            Phone = Optional(request.Phone),
            AccountTypeId = AccountType.FreeId,
            IsSuperAdmin = false,
            IsActive = true,
            CreatedDate = now,
            ModifiedDate = now
        };

        if (request.AccountKind == RegistrationAccountKind.Company)
        {
            var company = request.Company!;
            user.Company = new CompanyAccount
            {
                Name = company.Name.Trim(), Country = Optional(company.Country),
                Address = Optional(company.Address), WebsiteUrl = Optional(company.WebsiteUrl),
                AccountTypeId = AccountType.FreeId, SeatLimit = 1, IsActive = true,
                CreatedDate = now, ModifiedDate = now
            };
            // Owner is an application role. Company users inherit the company's Free plan.
            user.CompanyRole = CompanyRole.Owner;
            user.AccountTypeId = null;
        }

        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var (session, token) = await StartSessionAsync(user, request.ClientType, request.DeviceName, userAgent, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AuthOutcome(AuthFailure.None, user, session, token);
    }

    public static List<RecordValidationError> ValidateRegistration(RegisterRequest request, int minimumPasswordLength)
    {
        var errors = new List<RecordValidationError>();
        void Text(string field, string? value, int max, bool required = false)
        {
            if (required && string.IsNullOrWhiteSpace(value)) errors.Add(new(field, "required", "This field is required."));
            else if (value?.Trim().Length > max) errors.Add(new(field, "too_long", $"Use at most {max} characters."));
        }
        Text("displayName", request.DisplayName, 200, required: true);
        Text("phone", request.Phone, 40);
        if (!LooksLikeEmail(NormaliseEmail(request.Email))) errors.Add(new("email", "invalid_format", "Enter a valid email address."));
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < minimumPasswordLength)
            errors.Add(new("password", "too_short", $"Use at least {minimumPasswordLength} characters."));
        else if (request.Password.Length > 1024) errors.Add(new("password", "too_long", "Use at most 1024 characters."));
        if (!Enum.IsDefined(request.AccountKind)) errors.Add(new("accountKind", "invalid_type", "Choose Personal or Company."));
        if (!Enum.IsDefined(request.ClientType)) errors.Add(new("clientType", "invalid_type", "Unknown client type."));
        if (request.AccountKind == RegistrationAccountKind.Company)
        {
            Text("company.name", request.Company?.Name, 200, required: true);
            Text("company.country", request.Company?.Country, 100);
            Text("company.address", request.Company?.Address, 2000);
            Text("company.websiteUrl", request.Company?.WebsiteUrl, 2048);
            if (Optional(request.Company?.WebsiteUrl) is { } website &&
                (!Uri.TryCreate(website, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                 string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)))
                errors.Add(new("company.websiteUrl", "invalid_format", "Enter an http:// or https:// website URL."));
        }
        else if (request.Company is not null)
            errors.Add(new("company", "invalid_type", "Company details require a company account."));
        return errors;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<AuthOutcome> LoginAsync(
        string email,
        string password,
        ClientType clientType,
        string? deviceName,
        string? userAgent,
        string clientIp,
        CancellationToken cancellationToken)
    {
        var normalisedEmail = NormaliseEmail(email);

        // Checked before the password is even hashed. A throttled attempt must be the cheapest thing
        // the endpoint can do - if being blocked cost real work, the block would be the attack.
        var throttle = await _throttle.CheckAsync(normalisedEmail, clientIp, cancellationToken);

        if (throttle.IsThrottled)
        {
            SecurityLog.ThrottleApplied(_securityLog, throttle.Scope.ToString()!, clientIp, throttle.RetryAfter);
            return AuthOutcome.Throttled(throttle.RetryAfter);
        }

        var user = await _db.UserAccounts
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Email == normalisedEmail, cancellationToken);

        // Hash something either way, so "no such account" and "wrong password" take the same time.
        var verified = user is null
            ? VerifyAgainstDummy(password)
            : PasswordHasher.Verify(password ?? string.Empty, user.PasswordHash, user.PasswordSalt);

        if (user is null || !verified || !user.IsActive)
        {
            // Counted for unknown addresses too. Only counting real accounts would make the throttle
            // an oracle: "this address never locks out" answers the question the generic error
            // message exists to avoid.
            var failure = await _throttle.RecordFailureAsync(normalisedEmail, clientIp, cancellationToken);

            SecurityLog.FailedSignIn(_securityLog, clientIp, failure.IpFailures, failure.AccountFailures);

            if (failure.IsThrottled)
            {
                SecurityLog.ThrottleApplied(_securityLog, failure.Scope.ToString()!, clientIp, failure.RetryAfter);
            }

            // The caller is still told only that the credentials were wrong - the throttle state is
            // not reported until it actually blocks a request.
            return AuthOutcome.Fail(AuthFailure.InvalidCredentials);
        }

        // Whatever an attacker built up against this address, the owner signing in once clears it.
        // That is what keeps an account-targeted lockout survivable rather than a denial of service.
        await _throttle.ClearAsync(normalisedEmail, clientIp, cancellationToken);
        SecurityLog.SignedIn(_securityLog, user.Id, clientIp);

        var now = DateTime.UtcNow;
        var (session, token) = await StartSessionAsync(user, clientType, deviceName, userAgent, now, cancellationToken);

        return new AuthOutcome(AuthFailure.None, user, session, token);
    }

    /// <summary>Ends one device session. Every other device stays signed in.</summary>
    public async Task<bool> RevokeSessionAsync(long userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session is null || session.RevokedDate is not null)
        {
            return false;
        }

        session.RevokedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Signs out everywhere, optionally sparing the device that asked for it.</summary>
    public async Task<int> RevokeAllSessionsAsync(long userId, Guid? exceptSessionId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedDate == null && s.Id != exceptSessionId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedDate = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }

    public Task<List<UserSession>> ListActiveSessionsAsync(long userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return _db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedDate == null && s.ExpiresDate > now)
            .OrderByDescending(s => s.LastUsedDate)
            .ToListAsync(cancellationToken);
    }

    public Task<UserAccount?> FindUserAsync(long userId, CancellationToken cancellationToken) =>
        _db.UserAccounts.AsNoTracking().Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    /// <summary>
    /// Partial update of an account. Nulls mean "leave alone", which is why clearing the language
    /// preference needs its own flag - null and "stop remembering" are different intentions.
    /// </summary>
    public async Task<UserAccount?> UpdateProfileAsync(
        long userId,
        string? displayName,
        bool clearPreferredLanguage,
        string? preferredLanguage,
        CancellationToken cancellationToken)
    {
        var user = await _db.UserAccounts.Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName.Trim();
        }

        if (clearPreferredLanguage)
        {
            user.PreferredLanguage = null;
        }
        else if (preferredLanguage is not null)
        {
            user.PreferredLanguage = preferredLanguage;
        }

        user.ModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return user;
    }

    /// <summary>
    /// Mints one device credential. The raw token is returned to the caller and immediately forgotten
    /// here; only its hash is persisted.
    /// </summary>
    private async Task<(UserSession Session, string Token)> StartSessionAsync(
        UserAccount user,
        ClientType clientType,
        string? deviceName,
        string? userAgent,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await EnforceSessionLimitAsync(user.Id, now, cancellationToken);

        var token = SessionToken.Create();

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = SessionToken.Hash(token),
            ClientType = clientType,
            DeviceName = Truncate(deviceName, 200),
            UserAgent = Truncate(userAgent, 512),
            CreatedDate = now,
            LastUsedDate = now,
            ExpiresDate = now.AddDays(_options.SessionDays)
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return (session, token);
    }

    /// <summary>
    /// Keeps a runaway client - or a stolen password being used over and over - from accumulating
    /// unbounded live credentials. Least recently used sessions go first.
    /// </summary>
    private async Task EnforceSessionLimitAsync(long userId, DateTime now, CancellationToken cancellationToken)
    {
        var surplus = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedDate == null && s.ExpiresDate > now)
            .OrderByDescending(s => s.LastUsedDate)
            .Skip(Math.Max(0, _options.MaximumSessionsPerUser - 1))
            .ToListAsync(cancellationToken);

        foreach (var session in surplus)
        {
            session.RevokedDate = now;
        }
    }

    /// <summary>Always returns false. It exists purely to spend the same time a real verify would.</summary>
    private static bool VerifyAgainstDummy(string? password)
    {
        PasswordHasher.Verify(password ?? string.Empty, DummyCredential.Hash, DummyCredential.Salt);
        return false;
    }

    private static string NormaliseEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// A shape check, not a validity check. Whether an address can receive mail is settled by sending
    /// mail to it, not by a regular expression.
    /// </summary>
    private static bool LooksLikeEmail(string email)
    {
        var at = email.IndexOf('@');

        return email.Length is > 2 and <= 320
            && at > 0
            && at < email.Length - 1
            && email.IndexOf('@', at + 1) < 0
            && !email.Contains(' ');
    }

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maximumLength ? value : value[..maximumLength];
}
