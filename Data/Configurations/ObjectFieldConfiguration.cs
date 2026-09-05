using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class ObjectFieldConfiguration : IEntityTypeConfiguration<ObjectField>
{
    public void Configure(EntityTypeBuilder<ObjectField> builder)
    {
        builder.ToTable("ObjectFields");
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.ObjectField)
            .HasForeignKey<ObjectField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReferencedObjectDefinition).WithMany().HasForeignKey(x => x.ReferencedObjectDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
