using handytool_api.Models;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Data;

public class HandyToolDbContext : DbContext
{
    public HandyToolDbContext(DbContextOptions<HandyToolDbContext> options) : base(options)
    {
    }

    public DbSet<MasterCategoryTranslation> MasterCategoryTranslations => Set<MasterCategoryTranslation>();
    public DbSet<SubcategoryTranslation> SubcategoryTranslations => Set<SubcategoryTranslation>();
    public DbSet<ObjectDefinitionTranslation> ObjectDefinitionTranslations => Set<ObjectDefinitionTranslation>();
    public DbSet<FieldDefinitionTranslation> FieldDefinitionTranslations => Set<FieldDefinitionTranslation>();
    public DbSet<FieldOptionTranslation> FieldOptionTranslations => Set<FieldOptionTranslation>();

    public DbSet<ShortTextField> ShortTextFields => Set<ShortTextField>();
    public DbSet<LongTextField> LongTextFields => Set<LongTextField>();
    public DbSet<IntegerField> IntegerFields => Set<IntegerField>();
    public DbSet<DecimalField> DecimalFields => Set<DecimalField>();
    public DbSet<RangeField> RangeFields => Set<RangeField>();
    public DbSet<DateField> DateFields => Set<DateField>();
    public DbSet<TimeField> TimeFields => Set<TimeField>();
    public DbSet<DateTimeField> DateTimeFields => Set<DateTimeField>();
    public DbSet<DropdownField> DropdownFields => Set<DropdownField>();
    public DbSet<RadioGroupField> RadioGroupFields => Set<RadioGroupField>();
    public DbSet<ChecklistField> ChecklistFields => Set<ChecklistField>();
    public DbSet<MultiSelectField> MultiSelectFields => Set<MultiSelectField>();
    public DbSet<BooleanField> BooleanFields => Set<BooleanField>();
    public DbSet<MarkdownField> MarkdownFields => Set<MarkdownField>();
    public DbSet<ObjectField> ObjectFields => Set<ObjectField>();
    public DbSet<CollectionField> CollectionFields => Set<CollectionField>();

    public DbSet<MasterCategory> MasterCategories => Set<MasterCategory>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<CompanyAccount> CompanyAccounts => Set<CompanyAccount>();
    public DbSet<AccountType> AccountTypes => Set<AccountType>();

    public DbSet<ObjectDefinition> ObjectDefinitions => Set<ObjectDefinition>();

    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();

    public DbSet<FieldOption> FieldOptions => Set<FieldOption>();

    public DbSet<ObjectRecord> ObjectRecords => Set<ObjectRecord>();

    // --- Authentication: who someone is, and which devices are currently signed in as them. ---

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    /// <summary>Consecutive sign-in failures per address and per account. See <see cref="AuthThrottle"/>.</summary>
    public DbSet<AuthThrottle> AuthThrottles => Set<AuthThrottle>();

    // --- Analytics: which browser, which browsing session, and what happened. ---

    public DbSet<TrackingClient> TrackingClients => Set<TrackingClient>();

    public DbSet<TrackingClientUser> TrackingClientUsers => Set<TrackingClientUser>();

    public DbSet<TrackingSession> TrackingSessions => Set<TrackingSession>();

    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HandyToolDbContext).Assembly);
    }
}
