using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace handytool_api.Data.Configurations;
public class TrialUsageConfiguration : IEntityTypeConfiguration<TrialUsage>
{
    public void Configure(EntityTypeBuilder<TrialUsage> b)
    {
        b.ToTable("TrialUsage", t => t.HasCheckConstraint("CK_TrialUsage_Count", "\"CreatedCount\" >= 0"));
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100);
    }
}
