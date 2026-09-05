using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class MultiSelectFieldConfiguration : IEntityTypeConfiguration<MultiSelectField>
{
    public void Configure(EntityTypeBuilder<MultiSelectField> builder)
    {
        builder.ToTable("MultiSelectFields", t => t.HasCheckConstraint("CK_MultiSelectFields_Settings", """("MinimumSelections" IS NULL OR "MaximumSelections" IS NULL OR "MinimumSelections" <= "MaximumSelections") AND ("MinimumSelections" IS NULL OR "MinimumSelections" >= 0) AND ("MaximumSelections" IS NULL OR "MaximumSelections" >= 0)"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.MultiSelectField)
            .HasForeignKey<MultiSelectField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

    }
}
