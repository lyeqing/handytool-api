using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace handytool_api.Tests;

public class AdminTests
{
    [Theory]
    [InlineData(false, null, 401)]
    [InlineData(false, 1L, 403)]
    public void Admin_access_rejects_anonymous_and_ordinary_users(bool superuser, long? userId, int status)
    {
        var failure=Assert.Throws<ApiFailure>(() => AdminService.RequireSuperuser(new(userId,null,null,superuser,1,1)));
        Assert.Equal(status,failure.Status);
    }
    [Fact]
    public void Superuser_access_is_independent_of_company_role()
        => AdminService.RequireSuperuser(new(1,2,CompanyRole.Member,true,1,1));
    [Fact]
    public void Last_active_administrator_is_protected()
    {
        Assert.Equal(409,Assert.Throws<ApiFailure>(() => AdminService.ProtectLast(true,0,"superuser")).Status);
        AdminService.ProtectLast(true,1,"superuser");
        AdminService.ProtectLast(false,0,"superuser");
    }
    [Fact]
    public void Required_and_bounded_fields_are_validated()
    {
        Assert.Throws<ApiFailure>(() => AdminService.Text(" ",200,true));
        Assert.Throws<ApiFailure>(() => AdminService.Text(new string('x',41),40));
        Assert.Null(AdminService.Text(" ",40));
        Assert.Equal("Name",AdminService.Text(" Name ",200,true));
    }

    // Opt-in PostgreSQL check. Every inserted/updated row rolls back when the test finishes.
    [AdminDatabaseFact]
    public async Task Database_edits_preserve_membership_rules_and_category_references()
    {
        var root=Environment.GetEnvironmentVariable("HANDYTOOL_ADMIN_TEST_ROOT")!;
        var config=new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json",true).AddUserSecrets<HandyToolDbContext>(true).AddEnvironmentVariables().Build();
        await using var db=new HandyToolDbContext(new DbContextOptionsBuilder<HandyToolDbContext>().UseNpgsql(config.GetConnectionString("HandyTool")).Options);
        await using var transaction=await db.Database.BeginTransactionAsync();
        var access=new AccessService(db);
        await access.LockKeyAsync("admin:mutations",default);
        var service=new AdminService(db,access,NullLogger<AdminService>.Instance);
        var suffix=Guid.NewGuid().ToString("N");
        var company=new CompanyAccount{Name="Admin test "+suffix,SeatLimit=2,CreatedDate=DateTime.UtcNow,ModifiedDate=DateTime.UtcNow};
        var owner=new UserAccount{Email=suffix+"@example.test",DisplayName="Test owner",Company=company,CompanyRole=CompanyRole.Owner,AccountTypeId=null,
            PasswordHash="test",PasswordSalt="test",CreatedDate=DateTime.UtcNow,ModifiedDate=DateTime.UtcNow};
        db.Add(owner); await db.SaveChangesAsync();
        await db.Entry(owner).ReloadAsync();
        var edit=new AdminUserEdit(owner.DisplayName,owner.Email,null,null,company.Id,CompanyRole.Member,null,true,false,owner.ModifiedDate);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFailure>(() => service.UpdateUser(owner.Id,edit,default))).Status);
        await service.UpdateUser(owner.Id,edit with {CompanyRole=CompanyRole.Owner,Phone="123"},default);
        Assert.Equal("123",owner.Phone);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFailure>(() => service.UpdateUser(owner.Id,edit,default))).Status);
        await db.Entry(company).ReloadAsync();
        await service.UpdateCompany(company.Id,new(company.Name,"Australia",null,null,AccountType.FullId,2,null,true,company.ModifiedDate),default);
        Assert.Equal(AccountType.FullId,company.AccountTypeId);

        var category=new AdminCategoryEdit("Admin master "+suffix,"",true,9,null,[],null);
        await service.SaveCategory(null,false,category,default);
        var master=await db.MasterCategories.SingleAsync(x=>x.Name==category.Name);
        await service.SaveCategory(null,false,category with{Name="Other "+suffix},default);
        var other=await db.MasterCategories.SingleAsync(x=>x.Name=="Other "+suffix);
        await service.SaveCategory(null,true,category with{Name="Admin sub "+suffix,MasterCategoryId=master.Id},default);
        var sub=await db.Subcategories.SingleAsync(x=>x.Name=="Admin sub "+suffix);
        var definition=new ObjectDefinition{Name="Admin tool "+suffix,CreatedByUserId=owner.Id,MasterCategoryId=master.Id,SubcategoryId=sub.Id,
            CreatedDate=DateTime.UtcNow,ModifiedDate=DateTime.UtcNow};
        db.Add(definition);await db.SaveChangesAsync();
        await db.Entry(sub).ReloadAsync();
        await service.SaveCategory(sub.Id,true,category with {Name=sub.Name,MasterCategoryId=other.Id,ModifiedDate=sub.ModifiedDate,
            Translations=[new("zh-Hans","测试分类",null)]},default);
        await db.Entry(definition).ReloadAsync();
        Assert.Equal(other.Id,definition.MasterCategoryId);
        Assert.Equal(sub.Id,definition.SubcategoryId);
        Assert.Equal("测试分类",(await db.SubcategoryTranslations.SingleAsync(x=>x.SubcategoryId==sub.Id)).Name);
        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var originalList = System.Text.Json.JsonSerializer.SerializeToElement(
            await service.Categories(true, null, 0, default, master.Id), jsonOptions);
        Assert.Equal(0, originalList.GetProperty("total").GetInt32());
        var movedList = System.Text.Json.JsonSerializer.SerializeToElement(
            await service.Categories(true, null, 0, default, other.Id), jsonOptions);
        Assert.Equal(1, movedList.GetProperty("total").GetInt32());
        Assert.Equal(other.Id, movedList.GetProperty("parent").GetProperty("id").GetInt64());
        Assert.Equal(sub.Id, movedList.GetProperty("items")[0].GetProperty("id").GetInt64());
        var filteredList = System.Text.Json.JsonSerializer.SerializeToElement(
            await service.Categories(true, "no-match-"+suffix, 0, default, other.Id), jsonOptions);
        Assert.Equal(0, filteredList.GetProperty("total").GetInt32());
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiFailure>(
            () => service.Categories(true, null, 0, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiFailure>(
            () => service.Categories(true, null, 0, default, long.MinValue))).Status);
        await transaction.RollbackAsync();
    }
}

public sealed class AdminDatabaseFactAttribute : FactAttribute
{
    public AdminDatabaseFactAttribute()
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HANDYTOOL_ADMIN_TEST_ROOT")))
            Skip="Set HANDYTOOL_ADMIN_TEST_ROOT to opt into a rolled-back PostgreSQL integration test.";
    }
}
