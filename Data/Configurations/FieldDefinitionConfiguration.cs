using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class FieldDefinitionConfiguration : IEntityTypeConfiguration<FieldDefinition>
{
    public void Configure(EntityTypeBuilder<FieldDefinition> builder)
    {
        builder.ToTable("FieldDefinitions", t => t.HasCheckConstraint("CK_FieldDefinitions_FieldType", "\"FieldType\" BETWEEN 1 AND 16"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.FieldType).HasConversion<int>().IsRequired();

        builder.Property(x => x.IsRequired)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedDate).IsRequired();
        builder.Property(x => x.ModifiedDate).IsRequired();

        builder.HasIndex(x => x.ObjectDefinitionId);

        // A field key is unique inside its definition - it is the JSON property name in record values.
        builder.HasIndex(x => new { x.ObjectDefinitionId, x.Key }).IsUnique();

        builder.HasOne(x => x.ObjectDefinition)
            .WithMany(x => x.Fields)
            .HasForeignKey(x => x.ObjectDefinitionId)
            // Safe: deleting a definition is only permitted while it has no records (see ObjectRecord).
            .OnDelete(DeleteBehavior.Cascade);

    }
}
