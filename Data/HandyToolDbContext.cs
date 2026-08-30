using handytool_api.Models;
using Microsoft.EntityFrameworkCore;

namespace handytool_api.Data;

public class HandyToolDbContext : DbContext
{
    public HandyToolDbContext(DbContextOptions<HandyToolDbContext> options) : base(options)
    {
    }

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
