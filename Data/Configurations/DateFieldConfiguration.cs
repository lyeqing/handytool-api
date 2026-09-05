using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class DateFieldConfiguration : IEntityTypeConfiguration<DateField>
{
    public void Configure(EntityTypeBuilder<DateField> builder)
    {
        builder.ToTable("DateFields", t => t.HasCheckConstraint("CK_DateFields_Settings", """("MinimumDate" IS NULL OR "MaximumDate" IS NULL OR "MinimumDate" <= "MaximumDate")"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.DateField)
            .HasForeignKey<DateField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

    }
}
