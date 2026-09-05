using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class TimeFieldConfiguration : IEntityTypeConfiguration<TimeField>
{
    public void Configure(EntityTypeBuilder<TimeField> builder)
    {
        builder.ToTable("TimeFields", t => t.HasCheckConstraint("CK_TimeFields_Settings", """("MinimumTime" IS NULL OR "MaximumTime" IS NULL OR "MinimumTime" <= "MaximumTime") AND ("StepSeconds" IS NULL OR "StepSeconds" > 0)"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.TimeField)
            .HasForeignKey<TimeField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

    }
}
