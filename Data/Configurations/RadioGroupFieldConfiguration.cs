using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class RadioGroupFieldConfiguration : IEntityTypeConfiguration<RadioGroupField>
{
    public void Configure(EntityTypeBuilder<RadioGroupField> builder)
    {
        builder.ToTable("RadioGroupFields");
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.RadioGroupField)
            .HasForeignKey<RadioGroupField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

    }
}
