using System.Text.Json;
using handytool_api.Contracts;
using handytool_api.Data;
using handytool_api.Models;
using handytool_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static handytool_api.Tests.PropertyInspection;

namespace handytool_api.Tests;

public class DefinitionEditingTests
{
    [EditingDatabaseFact]
    public async Task Database_update_preserves_ids_and_rolls_back_incompatible_changes()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Environment.GetEnvironmentVariable("HANDYTOOL_EDIT_TEST_ROOT")!)
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", true)
            .AddUserSecrets<HandyToolDbContext>(true).AddEnvironmentVariables().Build();
        await using var db = new HandyToolDbContext(new DbContextOptionsBuilder<HandyToolDbContext>()
            .UseNpgsql(config.GetConnectionString("HandyTool")).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        await DefinitionUpdateService.LockAsync(db, true, default);
        var owner = new UserAccount { Email = Guid.NewGuid() + "@example.test", DisplayName = "Editing test",
            PasswordHash = "test", PasswordSalt = "test", CreatedDate = Now, ModifiedDate = Now };
        db.Add(owner); await db.SaveChangesAsync();
        var field = Field("name", "Name", FieldType.ShortText); field.ObjectDefinitionId = null;
        var definition = new ObjectDefinition { Name = Guid.NewGuid().ToString(), CreatedByUserId = owner.Id,
            CreatedDate = Now, ModifiedDate = Now, Fields = [field] };
        db.Add(definition); await db.SaveChangesAsync();
        var record = new ObjectRecord { ObjectDefinitionId = definition.Id, CreatedByUserId = owner.Id,
            CreatedDate = Now, ModifiedDate = Now, Values = JsonDocument.Parse("""{"name":"Alice"}""") };
        db.Add(record); await db.SaveChangesAsync();
        var fieldId = field.Id;
        var next = Field("name", "Full name", FieldType.ShortText, settings: """{"maximumLength":200}""");
        DefinitionUpdateService.MergeField(field, next, Request(field), Now.AddSeconds(1));
        var collection = Field("items", "Items", FieldType.Collection); collection.ObjectDefinitionId = null;
        collection.CollectionField!.ItemDefinition = Field("item", "Item", FieldType.Integer);
        collection.CollectionField.ItemDefinition.ObjectDefinitionId = null;
        definition.Fields.Add(collection);
        await db.SaveChangesAsync();
        var access = new AccessService(db); var loader = new DefinitionLoader(db, access);
        await DefinitionUpdateService.CheckStoredDataAsync(db, loader, definition.Id, default);
        var loaded = (await loader.LoadAsync(definition.Id, new(owner.Id, null, null, false, 1, 1), default))!;
        Assert.Equal(fieldId, loaded.Fields.Single(f => f.Key == "name").Id);
        Assert.Equal("Full name", loaded.Fields.Single(f => f.Key == "name").Name);
        Assert.True(loaded.Fields.Single(f => f.Key == "items").CollectionField!.ItemDefinitionId > 0);
        await tx.CreateSavepointAsync("compatible");
        field.ShortTextField!.MaximumLength = 2; await db.SaveChangesAsync();
        Assert.Equal("incompatible_definition", (await Assert.ThrowsAsync<ApiFailure>(() =>
            DefinitionUpdateService.CheckStoredDataAsync(db, loader, definition.Id, default))).Code);
        await tx.RollbackToSavepointAsync("compatible");
        Assert.Equal(200, await db.ShortTextFields.AsNoTracking().Where(f => f.FieldDefinitionId == fieldId).Select(f => f.MaximumLength).SingleAsync());
        // Exercise EF's actual revision predicate with two snapshots of the same row.
        db.ChangeTracker.Clear();
        var first = await db.ObjectRecords.SingleAsync(r => r.Id == record.Id);
        var stale = await db.ObjectRecords.AsNoTracking().SingleAsync(r => r.Id == record.Id);
        first.Title = "First writer"; first.Revision++; await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); db.Attach(stale);
        stale.Title = "Stale writer"; stale.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        await tx.RollbackAsync();
    }

    private static readonly DateTime Now = new(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc);
    private static CreateFieldDefinitionRequest Request(FieldDefinition f) => new(f.Key, f.Name, f.FieldType, Id: f.Id);

    [Fact]
    public void Editing_changes_labels_settings_and_translations_without_replacing_identity()
    {
        var old = Field("name", "Name", FieldType.ShortText);
        old.Id = 42;
        old.Translations.Add(new() { LanguageCode = "zh-Hans", Name = "旧名称" });
        var config = old.ShortTextField;
        var next = Field("name", "Full name", FieldType.ShortText, settings: """{"maximumLength":200}""");
        next.Translations.Add(new() { LanguageCode = "zh-Hans", Name = "姓名" });
        DefinitionUpdateService.MergeFields([old], [next], [Request(old)], Now);
        Assert.Equal(42, old.Id);
        Assert.Same(config, old.ShortTextField);
        Assert.Equal("Full name", old.Name);
        Assert.Equal(200, old.ShortTextField!.MaximumLength);
        Assert.Equal("姓名", Assert.Single(old.Translations).Name);
        Assert.Equal(Now, old.ModifiedDate);
    }

    [Theory]
    [InlineData("renamed", FieldType.ShortText)]
    [InlineData("name", FieldType.LongText)]
    public void Keys_and_types_cannot_be_replaced(string key, FieldType type)
    {
        var old = Field("name", "Name", FieldType.ShortText); old.Id = 42;
        var failure = Assert.Throws<ApiFailure>(() =>
            DefinitionUpdateService.MergeFields([old], [Field(key, "Name", type)], [Request(old)], Now));
        Assert.Equal("stable_field", failure.Code);
    }

    [Fact]
    public void Omitted_or_foreign_ids_cannot_delete_or_replace_fields()
    {
        var old = Field("name", "Name", FieldType.ShortText); old.Id = 42;
        Assert.Throws<ApiFailure>(() => DefinitionUpdateService.MergeFields([old], [], [], Now));
        Assert.Throws<ApiFailure>(() => DefinitionUpdateService.MergeFields([old], [old], [Request(old) with { Id = 99 }], Now));
    }

    [Fact]
    public void Option_labels_can_change_but_values_are_stable()
    {
        var old = Field("choice", "Choice", FieldType.Dropdown, options: [Option("a", "Old")]);
        old.Id = 42; old.Options.Single().Id = 7;
        var next = Field("choice", "Choice", FieldType.Dropdown, options: [Option("a", "New")]);
        var request = Request(old) with { Options = [new("a", "New", Id: 7)] };
        DefinitionUpdateService.MergeField(old, next, request, Now);
        Assert.Equal("New", old.Options.Single().Label);
        Assert.Equal(7, old.Options.Single().Id);
        next.Options.Single().Value = "changed";
        Assert.Equal("stable_option", Assert.Throws<ApiFailure>(() =>
            DefinitionUpdateService.MergeField(old, next, request, Now)).Code);
    }

    [Fact]
    public void Collection_item_keeps_its_identity_and_configuration()
    {
        var old = Field("items", "Items", FieldType.Collection); old.Id = 1;
        var item = Field("item", "Item", FieldType.Integer); item.Id = 2;
        old.CollectionField!.ItemDefinition = item; old.CollectionField.ItemDefinitionId = 2;
        var next = Field("items", "Items", FieldType.Collection);
        next.CollectionField!.ItemDefinition = Field("item", "Quantity", FieldType.Integer, settings: """{"minimum":0}""");
        DefinitionUpdateService.MergeField(old, next, Request(old) with { Item = Request(item) }, Now);
        Assert.Same(item, old.CollectionField.ItemDefinition);
        Assert.Equal(2, item.Id);
        Assert.Equal("Quantity", item.Name);
        Assert.Equal(0, item.IntegerField!.Minimum);
    }

    [Fact]
    public void Stale_or_missing_definition_tokens_are_rejected()
    {
        var definition = new ObjectDefinition { ModifiedDate = Now };
        DefinitionUpdateService.CheckVersion(definition, Now);
        Assert.Equal("definition_conflict", Assert.Throws<ApiFailure>(() =>
            DefinitionUpdateService.CheckVersion(definition, Now.AddTicks(-10))).Code);
        Assert.Throws<ApiFailure>(() => DefinitionUpdateService.CheckVersion(definition, default));
    }

    [Fact]
    public void Canonical_edit_values_do_not_overwrite_default_text_with_a_translation()
    {
        var field = Field("name", "Name", FieldType.ShortText, settings: """{"placeholder":"Default"}""");
        field.Translations.Add(new() { LanguageCode = "en", Name = "English name", Placeholder = "English placeholder" });
        var definition = new ObjectDefinition { Name = "Base", Fields = [field] };
        definition.Translations.Add(new() { LanguageCode = "en", Name = "English" });
        var editor = ObjectDefinitionResponse.Editor(definition, "en");
        Assert.Equal("Base", editor.Name);
        Assert.Equal("Name", editor.Fields![0].Name);
        Assert.Equal("Default", editor.Fields[0].Settings.GetProperty("placeholder").GetString());
        Assert.Equal("English name", editor.Fields[0].NameTranslations!["en"]);
        Assert.Equal("English", ObjectDefinitionResponse.WithFields(definition, "en").Name);
    }

    [Fact]
    public void Narrowing_constraints_or_deactivating_used_fields_is_rejected()
    {
        var field = Field("name", "Name", FieldType.ShortText);
        var definition = new ObjectDefinition { Fields = [field] };
        var record = new ObjectRecord { Values = JsonDocument.Parse("""{"name":"Alice"}""") };
        DefinitionUpdateService.CheckRecord(definition, record);
        field.ShortTextField!.MaximumLength = 2;
        Assert.Equal("incompatible_definition", Assert.Throws<ApiFailure>(() =>
            DefinitionUpdateService.CheckRecord(definition, record)).Code);
        field.ShortTextField.MaximumLength = null; field.IsActive = false;
        Assert.Throws<ApiFailure>(() => DefinitionUpdateService.CheckRecord(definition, record));
    }

    [Fact]
    public void Cycles_are_rejected_and_nested_dependents_are_found()
    {
        var root = new ObjectDefinition { Id = 1 };
        var child = new ObjectDefinition { Id = 2 };
        var field = Field("child", "Child", FieldType.Object);
        field.ObjectField!.ReferencedObjectDefinition = child; root.Fields.Add(field);
        Assert.True(DefinitionUpdateService.References(root, 2));
        DefinitionUpdateService.CheckGraph(root);
        var back = Field("back", "Back", FieldType.Object);
        back.ObjectField!.ReferencedObjectDefinition = root; child.Fields.Add(back);
        Assert.Equal("definition_cycle", Assert.Throws<ApiFailure>(() => DefinitionUpdateService.CheckGraph(root)).Code);
    }

    [Fact]
    public void Ef_tracks_new_collection_items_without_replacing_existing_rows()
    {
        using var db = new HandyToolDbContext(new DbContextOptionsBuilder<HandyToolDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        var definition = new ObjectDefinition { Id = 1 };
        db.Attach(definition);
        var field = Field("items", "Items", FieldType.Collection);
        field.ObjectDefinitionId = null;
        var item = Field("item", "Item", FieldType.ShortText); item.ObjectDefinitionId = null;
        field.CollectionField!.ItemDefinition = item;
        definition.Fields.Add(field);
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Added, db.Entry(field).State);
        Assert.Equal(EntityState.Added, db.Entry(item).State);
        Assert.Equal(EntityState.Added, db.Entry(item.ShortTextField!).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(definition).State);
    }
}

public sealed class EditingDatabaseFactAttribute : FactAttribute
{
    public EditingDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HANDYTOOL_EDIT_TEST_ROOT")))
            Skip = "Set HANDYTOOL_EDIT_TEST_ROOT to run the rolled-back PostgreSQL editing test.";
    }
}
