using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class DropdownFieldConfiguration : IEntityTypeConfiguration<DropdownField>
{
    public void Configure(EntityTypeBuilder<DropdownField> builder)
    {
        builder.ToTable("DropdownFields");
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.DropdownField)
            .HasForeignKey<DropdownField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Placeholder).HasMaxLength(2000);
    }
}
