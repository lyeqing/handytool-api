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

    [AdminDatabaseFact]
    public async Task Category_deletion_moves_tools_preserves_records_and_rolls_back()
    {
        var config = new ConfigurationBuilder().SetBasePath(Environment.GetEnvironmentVariable("HANDYTOOL_ADMIN_TEST_ROOT")!)
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", true)
            .AddUserSecrets<HandyToolDbContext>(true).AddEnvironmentVariables().Build();
        await using var db = new HandyToolDbContext(new DbContextOptionsBuilder<HandyToolDbContext>()
            .UseNpgsql(config.GetConnectionString("HandyTool")).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var access = new AccessService(db);
        await access.LockKeyAsync("admin:mutations", default);
        var service = new AdminService(db, access, NullLogger<AdminService>.Instance);
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var owner = new UserAccount { Email=suffix+"@example.test", DisplayName="Deletion test",
            PasswordHash="test", PasswordSalt="test", CreatedDate=now, ModifiedDate=now };
        var master = new MasterCategory { Name="Delete "+suffix, CreatedDate=now, ModifiedDate=now,
            Translations=[new() {LanguageCode="zh-Hans",Name="删除测试"}] };
        var other = new MasterCategory { Name="Keep "+suffix, CreatedDate=now, ModifiedDate=now };
        var first = new Subcategory { Name="First",MasterCategory=master,CreatedDate=now,ModifiedDate=now,
            Translations=[new() {LanguageCode="zh-Hans",Name="子分类"}] };
        var sibling = new Subcategory { Name="Sibling",MasterCategory=master,CreatedDate=now,ModifiedDate=now };
        db.AddRange(owner,master,other,first,sibling);
        await db.SaveChangesAsync();
        ObjectDefinition Tool(string name, long masterId, long? subId) => new() {
            Name=name+suffix, CreatedByUserId=owner.Id,MasterCategoryId=masterId,SubcategoryId=subId,
            CreatedDate=now,ModifiedDate=now };
        var tools = new[] {Tool("first",master.Id,first.Id),Tool("sibling",master.Id,sibling.Id),
            Tool("direct",master.Id,null),Tool("unrelated",other.Id,null)};
        db.AddRange(tools);
        await db.SaveChangesAsync();
        var records = tools.Select(tool => new ObjectRecord { ObjectDefinitionId=tool.Id,CreatedByUserId=owner.Id,
            Title="Preserve me",Values=System.Text.Json.JsonDocument.Parse("{\"answer\":42}"),
            CreatedDate=now,ModifiedDate=now }).ToArray();
        db.AddRange(records);
        await db.SaveChangesAsync();
        await db.Entry(first).ReloadAsync();
        await db.Entry(master).ReloadAsync();
        var firstRevision=first.ModifiedDate;
        var masterRevision=master.ModifiedDate;
        db.ChangeTracker.Clear();

        Assert.Equal(400,(await Assert.ThrowsAsync<ApiFailure>(() =>
            service.DeleteCategory(MasterCategory.UncategorizedId,false,now,default))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFailure>(() =>
            service.DeleteCategory(first.Id,true,firstRevision.AddDays(-1),default))).Status);
        Assert.True(await db.Subcategories.AnyAsync(x=>x.Id==first.Id));
        await service.DeleteCategory(first.Id,true,firstRevision,default);
        Assert.False(await db.Subcategories.AnyAsync(x=>x.Id==first.Id));
        Assert.False(await db.SubcategoryTranslations.AnyAsync(x=>x.SubcategoryId==first.Id));
        var moved = await db.ObjectDefinitions.AsNoTracking().SingleAsync(x=>x.Id==tools[0].Id);
        Assert.Equal(MasterCategory.UncategorizedId,moved.MasterCategoryId);
        Assert.Null(moved.SubcategoryId);
        Assert.Equal(master.Id,(await db.ObjectDefinitions.AsNoTracking().SingleAsync(x=>x.Id==tools[1].Id)).MasterCategoryId);

        await transaction.CreateSavepointAsync("before_master_delete");
        await service.DeleteCategory(master.Id,false,masterRevision,default);
        Assert.False(await db.MasterCategories.AnyAsync(x=>x.Id==master.Id));
        Assert.False(await db.Subcategories.AnyAsync(x=>x.MasterCategoryId==master.Id));
        Assert.False(await db.MasterCategoryTranslations.AnyAsync(x=>x.MasterCategoryId==master.Id));
        foreach(var tool in tools.Take(3)) {
            var row=await db.ObjectDefinitions.AsNoTracking().SingleAsync(x=>x.Id==tool.Id);
            Assert.Equal(MasterCategory.UncategorizedId,row.MasterCategoryId);
            Assert.Null(row.SubcategoryId);
        }
        Assert.Equal(other.Id,(await db.ObjectDefinitions.AsNoTracking().SingleAsync(x=>x.Id==tools[3].Id)).MasterCategoryId);
        foreach(var record in records) {
            var row=await db.ObjectRecords.AsNoTracking().SingleAsync(x=>x.Id==record.Id);
            Assert.Equal(record.ObjectDefinitionId,row.ObjectDefinitionId);
            Assert.Equal("Preserve me",row.Title);
            Assert.Equal(42,row.Values.RootElement.GetProperty("answer").GetInt32());
            Assert.Equal(1,row.Revision);
        }
        await transaction.RollbackToSavepointAsync("before_master_delete");
        Assert.True(await db.MasterCategories.AnyAsync(x=>x.Id==master.Id));
        Assert.True(await db.Subcategories.AnyAsync(x=>x.Id==sibling.Id));
        var restored=await db.ObjectDefinitions.AsNoTracking().SingleAsync(x=>x.Id==tools[1].Id);
        Assert.Equal(master.Id,restored.MasterCategoryId);
        Assert.Equal(sibling.Id,restored.SubcategoryId);
        await transaction.RollbackAsync();
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

        company.SeatLimit=3;
        UserAccount TestUser(string name, CompanyRole? role) => new() {
            Email=name.Replace(" ","").ToLowerInvariant()+suffix+"@example.test",DisplayName=name,
            CompanyId=role is null?null:company.Id,CompanyRole=role,AccountTypeId=role is null?AccountType.FreeId:null,
            PasswordHash="test",PasswordSalt="test",CreatedDate=DateTime.UtcNow.AddDays(1),ModifiedDate=DateTime.UtcNow };
        var admin=TestUser("Z admin",CompanyRole.Admin);
        var member=TestUser("A member",CompanyRole.Member);
        var personal=TestUser("B personal",null);
        db.AddRange(admin,member,personal);
        await db.SaveChangesAsync();
        var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        System.Text.Json.JsonElement Json(object value) => System.Text.Json.JsonSerializer.SerializeToElement(value,options);
        var companyUsers=Json(await service.Users(suffix,0,default,company.Id));
        Assert.Equal(3,companyUsers.GetProperty("total").GetInt32());
        Assert.Equal(member.Id,companyUsers.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(company.Name,companyUsers.GetProperty("items")[0].GetProperty("companyName").GetString());
        var ownerFirst=Json(await service.Users(suffix,0,default,company.Id,false,"owners"));
        Assert.Equal(owner.Id,ownerFirst.GetProperty("items")[0].GetProperty("id").GetInt64());
        var adminFirst=Json(await service.Users(suffix,0,default,company.Id,false,"admins"));
        Assert.Equal(admin.Id,adminFirst.GetProperty("items")[0].GetProperty("id").GetInt64());
        var descending=Json(await service.Users(suffix,0,default,company.Id,false,"name-desc"));
        Assert.Equal(admin.Id,descending.GetProperty("items")[0].GetProperty("id").GetInt64());
        var newest=Json(await service.Users(suffix,0,default,null,false,"newest"));
        Assert.Equal(personal.Id,newest.GetProperty("items")[0].GetProperty("id").GetInt64());
        var personalOnly=Json(await service.Users(suffix,0,default,null,true));
        Assert.Equal(1,personalOnly.GetProperty("total").GetInt32());
        Assert.Equal(personal.Id,personalOnly.GetProperty("items")[0].GetProperty("id").GetInt64());
        var secondPage=Json(await service.Users(suffix,1,default,company.Id,false,"owners"));
        Assert.Equal(3,secondPage.GetProperty("total").GetInt32());
        Assert.Equal(2,secondPage.GetProperty("items").GetArrayLength());
        Assert.Equal(member.Id,secondPage.GetProperty("items")[0].GetProperty("id").GetInt64());
        var oneCompany=Json(await service.Companies(null,0,default,company.Id));
        Assert.Equal(1,oneCompany.GetProperty("total").GetInt32());
        Assert.Equal(company.Id,oneCompany.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(400,(await Assert.ThrowsAsync<ApiFailure>(()=>service.Users(null,0,default,company.Id,true))).Status);
        Assert.Equal(400,(await Assert.ThrowsAsync<ApiFailure>(()=>service.Users(null,0,default,null,false,"invalid"))).Status);
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
