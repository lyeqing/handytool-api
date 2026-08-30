using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class AuthThrottleConfiguration : IEntityTypeConfiguration<AuthThrottle>
{
    public void Configure(EntityTypeBuilder<AuthThrottle> builder)
    {
        builder.ToTable("AuthThrottles");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.Scope)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.KeyHash)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FailedCount).IsRequired();
        builder.Property(x => x.FirstFailedAt).IsRequired();
        builder.Property(x => x.LastFailedAt).IsRequired();

        // Every sign-in attempt reads this twice, so it has to be one index hit each time. Unique
        // because two counters for the same key would each see half the failures and neither trip.
        builder.HasIndex(x => new { x.Scope, x.KeyHash }).IsUnique();

        // For sweeping out cold counters. Nothing does that yet - the rows are tiny and self-limiting -
        // but the index means a cleanup job will not have to scan the table.
        builder.HasIndex(x => x.LastFailedAt);

        // No foreign key to UserAccounts on purpose: the account scope counts attempts against
        // addresses that were never registered, which is what stops the table from becoming a way to
        // ask which emails exist.
    }
}
