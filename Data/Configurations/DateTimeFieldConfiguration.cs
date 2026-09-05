using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class DateTimeFieldConfiguration : IEntityTypeConfiguration<DateTimeField>
{
    public void Configure(EntityTypeBuilder<DateTimeField> builder)
    {
        builder.ToTable("DateTimeFields", t => t.HasCheckConstraint("CK_DateTimeFields_Settings", """("MinimumDateTime" IS NULL OR "MaximumDateTime" IS NULL OR "MinimumDateTime" <= "MaximumDateTime")"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.DateTimeField)
            .HasForeignKey<DateTimeField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

    }
}
