using System.Text.Json;
using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Services;
using handytool_api.Validation;
using Microsoft.EntityFrameworkCore;
using static handytool_api.Tests.PropertyInspection;

namespace handytool_api.Tests;

public class RecordEditingTests
{
    [Fact]
    public void Record_revision_is_an_ef_concurrency_token()
    {
        using var db = new HandyToolDbContext(new DbContextOptionsBuilder<HandyToolDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.True(db.Model.FindEntityType(typeof(ObjectRecord))!.FindProperty(nameof(ObjectRecord.Revision))!.IsConcurrencyToken);
    }

    [Fact]
    public void Shared_company_read_access_does_not_grant_edit_access()
    {
        var record = new ObjectRecord { CreatedByUserId = 1, CompanyId = 2, Visibility = RecordVisibility.Company };
        var member = new AccessActor(3, 2, CompanyRole.Member, false, 1, 1);
        Assert.True(AccessService.CanReadRecord(member, record));
        Assert.False(AccessService.CanWriteRecord(member, record));
        Assert.True(AccessService.CanWriteRecord(member with { UserId = 1 }, record));
        Assert.True(AccessService.CanWriteRecord(member with { Role = CompanyRole.Admin }, record));
        Assert.False(AccessService.CanWriteRecord(member with { CompanyId = 9, Role = CompanyRole.Admin }, record));
    }

    [Fact]
    public void Trial_records_can_only_be_edited_with_the_owning_device()
    {
        var record = new ObjectRecord { AnonymousDeviceHash = "owner" };
        var anonymous = new AccessActor(null, null, null, false, 0, 1);
        Assert.True(AccessService.CanWriteRecord(anonymous, record, "owner"));
        Assert.False(AccessService.CanWriteRecord(anonymous, record, "another-device"));
    }

    [Fact]
    public void Response_preserves_revision_nulls_false_and_nested_json_types()
    {
        const string json = """{"enabled":false,"notes":null,"items":[{"count":2,"tags":["a"]}]}""";
        var record = new ObjectRecord { Revision = 12, Values = JsonDocument.Parse(json) };
        var response = ObjectRecordResponse.From(record);
        Assert.Equal(12, response.Revision);
        Assert.Equal(JsonValueKind.False, response.Values.GetProperty("enabled").ValueKind);
        Assert.Equal(JsonValueKind.Null, response.Values.GetProperty("notes").ValueKind);
        Assert.Equal(2, response.Values.GetProperty("items")[0].GetProperty("count").GetInt32());
    }

    [Fact]
    public void Nested_edited_record_values_receive_typed_validation()
    {
        var quantity = Field("quantity", "Quantity", FieldType.Integer, isRequired: true);
        var child = new ObjectDefinition { Id = 2, Fields = [quantity] };
        var item = Field("item", "Item", FieldType.Object);
        item.ObjectField!.ReferencedObjectDefinition = child;
        var items = Field("items", "Items", FieldType.Collection);
        items.CollectionField!.ItemDefinition = item;
        Assert.True(RecordValueValidator.Validate([items], Json("""{"items":[{"quantity":3}]}""")).IsValid);
        Assert.False(RecordValueValidator.Validate([items], Json("""{"items":[{"quantity":"3"}]}""")).IsValid);
    }
}
