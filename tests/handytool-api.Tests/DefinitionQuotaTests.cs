using handytool_api.Models;
using handytool_api.Services;

namespace handytool_api.Tests;

public class DefinitionQuotaTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 20)]
    [InlineData(3, 100)]
    public void Company_admins_private_definitions_use_personal_allowance(int level, long limit)
    {
        var actor = new AccessActor(7, 42, CompanyRole.Admin, false, level, 10);
        var scope = AccessService.DefinitionQuotaActor(actor, DefinitionVisibility.Private);

        Assert.Null(scope.CompanyId);
        Assert.Equal(actor.UserId, scope.UserId);
        Assert.Equal(limit, PlanLimits.Definitions(scope));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 50)]
    [InlineData(3, 1000)]
    public void Company_definitions_keep_shared_allowance(int level, long limit)
    {
        var actor = new AccessActor(7, 42, CompanyRole.Admin, false, level, 10);
        var scope = AccessService.DefinitionQuotaActor(actor, DefinitionVisibility.Company);

        Assert.Equal(actor, scope);
        Assert.Equal(limit, PlanLimits.Definitions(scope));
    }

    [Fact]
    public void Personal_scope_does_not_remove_inactive_company_restriction()
    {
        var actor = new AccessActor(7, 42, CompanyRole.Admin, false, 3, 10, CompanyActive: false);
        var scope = AccessService.DefinitionQuotaActor(actor, DefinitionVisibility.Private);
        Assert.False(scope.CompanyActive);
        Assert.False(scope.CanCreateDefinition);
    }

    [Fact]
    public void Superadmin_remains_exempt()
    {
        var actor = new AccessActor(7, 42, CompanyRole.Admin, true, 3, 10);
        var scope = AccessService.DefinitionQuotaActor(actor, DefinitionVisibility.Public);
        Assert.Null(scope.CompanyId);
        Assert.Equal(long.MaxValue, PlanLimits.Definitions(scope));
    }
}
