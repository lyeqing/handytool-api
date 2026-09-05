using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class RangeFieldConfiguration : IEntityTypeConfiguration<RangeField>
{
    public void Configure(EntityTypeBuilder<RangeField> builder)
    {
        builder.ToTable("RangeFields", t => t.HasCheckConstraint("CK_RangeFields_Settings", """("Minimum" IS NULL OR "Maximum" IS NULL OR "Minimum" <= "Maximum") AND ("Step" IS NULL OR "Step" > 0)"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.RangeField)
            .HasForeignKey<RangeField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

    }
}
