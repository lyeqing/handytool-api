using System.Text.Json;
using handytool_api.Models;
using handytool_api.Security;
using handytool_api.Services;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Data;

/// <summary>Explicit Development-only command; never migrates, deletes, or resets existing data.</summary>
public static class DevelopmentSeeder
{
    public static async Task SeedAsync(HandyToolDbContext db, IConfiguration config)
    {
        var password = config["DevelopmentSeed:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("Set DevelopmentSeed:Password to a demo password of at least 12 characters.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await new AccessService(db).LockKeyAsync("development-seed:v1", CancellationToken.None);
        const string email = "demo@handytool.test";
        var user = await db.UserAccounts.SingleOrDefaultAsync(u => u.Email == email);
        if (user is not null)
        {
            // This command commits the whole fixture atomically. An existing identity is never altered.
            return;
        }
        var now = DateTime.UtcNow;
        var (hash, salt) = PasswordHasher.Hash(password);
        user = new UserAccount { Email = email, DisplayName = "Demo User", PasswordHash = hash, PasswordSalt = salt,
            AccountTypeId = AccountType.FreeId, IsActive = true, CreatedDate = now, ModifiedDate = now };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();
        var samples = new[]
        {
            (Category: "Home & Property", Zh: "家居与房产", Description: "Keep everyday home details in order.",
                ZhDescription: "轻松整理家居与房产信息。", Tool: "Home checklist", ZhTool: "家居检查清单", Record: "Weekend home check", ZhLabel: "备注"),
            (Category: "Work & Business", Zh: "工作与业务", Description: "Make room for the work that matters.",
                ZhDescription: "整理工作信息，专注重要事务。", Tool: "Project notes", ZhTool: "项目笔记", Record: "Website launch notes", ZhLabel: "备注"),
            (Category: "Everyday Life", Zh: "日常生活", Description: "A little organisation for your everyday plans.",
                ZhDescription: "有条理地安排日常计划。", Tool: "Travel checklist", ZhTool: "旅行清单", Record: "Next trip essentials", ZhLabel: "备注")
        };
        foreach (var sample in samples)
        {
            var category = await db.MasterCategories.SingleOrDefaultAsync(c => c.Name == sample.Category);
            if (category is null)
            {
                category = new MasterCategory { Name = sample.Category, Description = sample.Description,
                    IsActive = true, CreatedDate = now, ModifiedDate = now };
                category.Translations.Add(new MasterCategoryTranslation { LanguageCode = "zh-Hans", Name = sample.Zh, Description = sample.ZhDescription });
                db.MasterCategories.Add(category);
            }
            var definition = new ObjectDefinition { Name = sample.Tool, Description = sample.Description,
                CreatedByUserId = user.Id, MasterCategory = category, Visibility = DefinitionVisibility.Public,
                RequiredAccessLevel = 0, IsActive = true, CreatedDate = now, ModifiedDate = now };
            definition.Translations.Add(new ObjectDefinitionTranslation { LanguageCode = "zh-Hans", Name = sample.ZhTool, Description = sample.ZhDescription });
            var field = new FieldDefinition { Key = "notes", Name = "Notes", FieldType = FieldType.LongText,
                IsActive = true, CreatedDate = now, ModifiedDate = now };
            field.Translations.Add(new FieldDefinitionTranslation { LanguageCode = "zh-Hans", Name = sample.ZhLabel });
            FieldConfiguration.Apply(field, null);
            definition.Fields.Add(field);
            db.ObjectDefinitions.Add(definition);
            db.ObjectRecords.Add(new ObjectRecord { ObjectDefinition = definition, CreatedByUserId = user.Id,
                Title = sample.Record, Description = "Sample record for testing the homepage.",
                Values = JsonDocument.Parse("{\"notes\":\"Demo content — replace with your own notes.\"}"),
                CreatedDate = now, ModifiedDate = now });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
