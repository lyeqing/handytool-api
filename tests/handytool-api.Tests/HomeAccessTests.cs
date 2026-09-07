using handytool_api.Models;
using handytool_api.Services;

namespace handytool_api.Tests;

public class HomeAccessTests
{
    [Fact]
    public void Anonymous_catalogue_excludes_private_company_and_paid_definitions()
    {
        var actor = new AccessActor(null, null, null, false, 0, 0);
        Assert.True(AccessService.CanReadDefinition(actor, new() { Visibility = DefinitionVisibility.Public }));
        Assert.False(AccessService.CanReadDefinition(actor, new() { Visibility = DefinitionVisibility.Private, CreatedByUserId = 5 }));
        Assert.False(AccessService.CanReadDefinition(actor, new() { Visibility = DefinitionVisibility.Company, CompanyId = 7 }));
        Assert.False(AccessService.CanReadDefinition(actor, new() { Visibility = DefinitionVisibility.Public, RequiredAccessLevel = 1 }));
    }

    [Fact]
    public void Personal_records_are_not_readable_by_another_user()
    {
        var record = new ObjectRecord { CreatedByUserId = 5 };
        Assert.True(AccessService.CanReadRecord(new(5, null, null, false, 1, 1), record));
        Assert.False(AccessService.CanReadRecord(new(6, null, null, false, 1, 1), record));
    }

    [Theory]
    [InlineData(-1, 0, 0, 1)]
    [InlineData(12, 12, 12, 12)]
    [InlineData(0, 1000, 0, 50)]
    public void Record_pages_are_bounded(int skip, int take, int expectedSkip, int expectedTake)
    {
        Assert.Equal((expectedSkip, expectedTake), HomeService.PageBounds(skip, take));
    }
}
