using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class FieldOptionConfiguration : IEntityTypeConfiguration<FieldOption>
{
    public void Configure(EntityTypeBuilder<FieldOption> builder)
    {
        builder.ToTable("FieldOptions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Label)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedDate).IsRequired();
        builder.Property(x => x.ModifiedDate).IsRequired();

        builder.HasIndex(x => x.FieldDefinitionId);

        // Option values are the stable tokens stored in record JSON.
        builder.HasIndex(x => new { x.FieldDefinitionId, x.Value }).IsUnique();

        builder.HasOne(x => x.FieldDefinition)
            .WithMany(x => x.Options)
            .HasForeignKey(x => x.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
