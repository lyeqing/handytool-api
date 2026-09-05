using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class CollectionFieldConfiguration : IEntityTypeConfiguration<CollectionField>
{
    public void Configure(EntityTypeBuilder<CollectionField> builder)
    {
        builder.ToTable("CollectionFields", t => t.HasCheckConstraint("CK_CollectionFields_Settings", """("MinimumItems" IS NULL OR "MaximumItems" IS NULL OR "MinimumItems" <= "MaximumItems") AND ("MinimumItems" IS NULL OR "MinimumItems" >= 0) AND ("MaximumItems" IS NULL OR "MaximumItems" >= 0) AND ("ItemDefinitionId" <> "FieldDefinitionId")"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.CollectionField)
            .HasForeignKey<CollectionField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ItemDefinition).WithMany().HasForeignKey(x => x.ItemDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
