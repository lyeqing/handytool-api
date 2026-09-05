using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class BooleanFieldConfiguration : IEntityTypeConfiguration<BooleanField>
{
    public void Configure(EntityTypeBuilder<BooleanField> builder)
    {
        builder.ToTable("BooleanFields");
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.BooleanField)
            .HasForeignKey<BooleanField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

    }
}
