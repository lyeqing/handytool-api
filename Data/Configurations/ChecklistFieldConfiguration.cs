using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class ChecklistFieldConfiguration : IEntityTypeConfiguration<ChecklistField>
{
    public void Configure(EntityTypeBuilder<ChecklistField> builder)
    {
        builder.ToTable("ChecklistFields", t => t.HasCheckConstraint("CK_ChecklistFields_Settings", """("MinimumSelections" IS NULL OR "MaximumSelections" IS NULL OR "MinimumSelections" <= "MaximumSelections") AND ("MinimumSelections" IS NULL OR "MinimumSelections" >= 0) AND ("MaximumSelections" IS NULL OR "MaximumSelections" >= 0)"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.ChecklistField)
            .HasForeignKey<ChecklistField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

    }
}
