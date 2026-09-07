using handytool_api.Models;
namespace handytool_api.Contracts;

public sealed record AdminUserEdit(string DisplayName, string Email, string? Phone, string? PreferredLanguage,
    long? CompanyId, CompanyRole? CompanyRole, int? AccountTypeId, bool IsActive, bool IsSuperAdmin, DateTime ModifiedDate);
public sealed record AdminCompanyEdit(string Name, string? Country, string? Address, string? WebsiteUrl,
    int AccountTypeId, int SeatLimit, DateTime? ExpiresAt, bool IsActive, DateTime ModifiedDate);
public sealed record AdminTranslation(string LanguageCode, string? Name, string? Description);
public sealed record AdminCategoryEdit(string Name, string? Description, bool IsActive, int DisplayOrder,
    long? MasterCategoryId, List<AdminTranslation>? Translations, DateTime? ModifiedDate);
public sealed record AdminUserRow(long Id, string DisplayName, string Email, string? Phone, string? PreferredLanguage,
    long? CompanyId, CompanyRole? CompanyRole, int? AccountTypeId, bool IsActive, bool IsSuperAdmin, DateTime ModifiedDate);
public sealed record AdminCompanyRow(long Id, string Name, string? Country, string? Address, string? WebsiteUrl,
    int AccountTypeId, int SeatLimit, DateTime? ExpiresAt, bool IsActive, DateTime ModifiedDate);
public sealed record AdminCategoryRow(long Id, string Name, string Description, bool IsActive, int DisplayOrder,
    long? MasterCategoryId, List<AdminTranslation> Translations, DateTime ModifiedDate);

