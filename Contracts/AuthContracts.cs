using handytool_api.Models;

namespace handytool_api.Contracts;

// ---------- Requests ----------

public enum RegistrationAccountKind { Personal, Company }

public sealed record CompanyRegistrationRequest(string Name, string? Country = null,
    string? Address = null, string? WebsiteUrl = null);

/// <summary>
/// <paramref name="DeviceName"/> and <paramref name="ClientType"/> only label the session in the
/// user's device list. They carry no authority: nothing is trusted from them.
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName = null,
    string? DeviceName = null,
    ClientType ClientType = ClientType.Web,
    string? Phone = null,
    RegistrationAccountKind AccountKind = RegistrationAccountKind.Personal,
    CompanyRegistrationRequest? Company = null);

public sealed record LoginRequest(
    string Email,
    string Password,
    string? DeviceName = null,
    ClientType ClientType = ClientType.Web);

// ---------- Responses ----------

/// <summary>
/// The only time the raw <paramref name="Token"/> ever exists outside the client. The database holds
/// nothing but its hash, so a lost token cannot be recovered - only replaced by signing in again.
/// </summary>
public sealed record AuthenticatedResponse(
    string Token,
    DateTime ExpiresDate,
    UserResponse User);

public sealed record UserResponse(
    long Id,
    string Email,
    string DisplayName,
    // Null means "follow the browser" rather than "English".
    string? PreferredLanguage,
    DateTime CreatedDate,
    string? Phone = null,
    long? CompanyId = null,
    CompanyRole? CompanyRole = null, bool IsSuperAdmin = false, string? CompanyName = null)
{
    public static UserResponse From(UserAccount user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.PreferredLanguage,
        user.CreatedDate,
        user.Phone,
        user.CompanyId,
        user.CompanyRole, user.IsSuperAdmin, user.Company?.Name);
}

/// <summary>
/// Partial update of the signed-in account. Every property is optional: omitted leaves the value
/// alone. To go back to following the browser, send preferredLanguage as an empty string.
/// </summary>
public sealed record UpdateProfileRequest(
    string? DisplayName = null,
    string? PreferredLanguage = null);

/// <summary>One signed-in device. Deliberately contains no part of the token.</summary>
public sealed record UserSessionResponse(
    Guid Id,
    ClientType ClientType,
    string? DeviceName,
    DateTime CreatedDate,
    DateTime LastUsedDate,
    DateTime ExpiresDate,
    bool IsCurrent)
{
    public static UserSessionResponse From(UserSession session, Guid currentSessionId) => new(
        session.Id,
        session.ClientType,
        session.DeviceName,
        session.CreatedDate,
        session.LastUsedDate,
        session.ExpiresDate,
        session.Id == currentSessionId);
}
