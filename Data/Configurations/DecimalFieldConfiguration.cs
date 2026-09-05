using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class DecimalFieldConfiguration : IEntityTypeConfiguration<DecimalField>
{
    public void Configure(EntityTypeBuilder<DecimalField> builder)
    {
        builder.ToTable("DecimalFields", t => t.HasCheckConstraint("CK_DecimalFields_Settings", """("Minimum" IS NULL OR "Maximum" IS NULL OR "Minimum" <= "Maximum") AND ("Step" IS NULL OR "Step" > 0)"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.DecimalField)
            .HasForeignKey<DecimalField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Placeholder).HasMaxLength(2000);
    }
}
