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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HandyToolDbContext).Assembly);
    }
}
