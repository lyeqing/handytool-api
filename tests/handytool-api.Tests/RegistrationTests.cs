using handytool_api.Contracts;
using handytool_api.Services;

namespace handytool_api.Tests;

public class RegistrationTests
{
    private static RegisterRequest Personal => new("registration@example.test", "test-password", "Test User");

    [Fact]
    public void Personal_registration_does_not_require_phone()
        => Assert.Empty(AuthService.ValidateRegistration(Personal, 8));

    [Fact]
    public void Company_registration_only_additionally_requires_company_name()
        => Assert.Empty(AuthService.ValidateRegistration(Personal with
        {
            AccountKind = RegistrationAccountKind.Company,
            Company = new("Test Company")
        }, 8));

    [Fact]
    public void Company_registration_requires_user_and_company_names()
    {
        var errors = AuthService.ValidateRegistration(Personal with
        {
            DisplayName = " ", AccountKind = RegistrationAccountKind.Company
        }, 8);
        Assert.Contains(errors, e => e.FieldKey == "displayName" && e.ErrorCode == "required");
        Assert.Contains(errors, e => e.FieldKey == "company.name" && e.ErrorCode == "required");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.test")]
    [InlineData("https://user:password@example.test")]
    [InlineData("example.test")]
    public void Invalid_company_websites_are_rejected(string website)
        => Assert.Contains(AuthService.ValidateRegistration(Personal with
        {
            AccountKind = RegistrationAccountKind.Company,
            Company = new("Test Company", WebsiteUrl: website)
        }, 8), e => e.FieldKey == "company.websiteUrl");

    [Fact]
    public void Personal_account_cannot_include_company_details()
        => Assert.Contains(AuthService.ValidateRegistration(Personal with { Company = new("Company") }, 8),
            e => e.FieldKey == "company");

    [Fact]
    public void Oversized_optional_contact_fields_are_rejected()
    {
        var errors = AuthService.ValidateRegistration(Personal with
        {
            Phone = new string('1', 41), AccountKind = RegistrationAccountKind.Company,
            Company = new("Company", new string('c', 101), new string('a', 2001))
        }, 8);
        Assert.Contains(errors, e => e.FieldKey == "phone");
        Assert.Contains(errors, e => e.FieldKey == "company.country");
        Assert.Contains(errors, e => e.FieldKey == "company.address");
    }
}
