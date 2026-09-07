namespace handytool_api.Models;

/// <summary>
/// A person who can sign in. <see cref="Id"/> is the stable account identity used everywhere else in
/// the system - it is what <see cref="ObjectDefinition.CreatedByUserId"/> points at and what analytics records
/// as <see cref="AnalyticsEvent.UserId"/>. It never changes, and no credential is ever used in its place.
/// </summary>
public class UserAccount
{
    // Database columns
    public long Id { get; set; }

    public long? CompanyId { get; set; }

    /// <summary>Member, Admin, or Owner for company users; null for independent users.</summary>
    public CompanyRole? CompanyRole { get; set; }

    public bool IsSuperAdmin { get; set; }

    /// <summary>Null for company users, who inherit the company plan.</summary>
    public int? AccountTypeId { get; set; } = AccountType.FreeId;

    /// <summary>Stored lowercase and uniquely indexed - the login identifier.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Base64 PBKDF2 derived key. See <see cref="Security.PasswordHasher"/>.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Base64 per-user random salt. Never shared between accounts.</summary>
    public string PasswordSalt { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>
    /// BCP-47 tag, or null to follow whatever the browser asks for. Stored on the account rather than
    /// only in a cookie so the choice follows the person to another device.
    /// </summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>A deactivated account keeps its data but cannot sign in and cannot use its sessions.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    // Relationships — not additional database columns
    public CompanyAccount? Company { get; set; }

    public AccountType? AccountType { get; set; }

    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}
