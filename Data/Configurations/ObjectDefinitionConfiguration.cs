using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class ObjectDefinitionConfiguration : IEntityTypeConfiguration<ObjectDefinition>
{
    public void Configure(EntityTypeBuilder<ObjectDefinition> builder)
    {
        builder.ToTable("ObjectDefinitions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(2000)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.NameTranslations)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(x => x.DescriptionTranslations)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(x => x.CreatedDate).IsRequired();
        builder.Property(x => x.ModifiedDate).IsRequired();

        builder.HasIndex(x => x.UserId);

        // One account cannot have two definitions with the same name.
        builder.HasIndex(x => new { x.UserId, x.Name }).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ObjectDefinitions_NameTranslations_IsObject",
            "jsonb_typeof(\"NameTranslations\") = 'object'"));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ObjectDefinitions_DescriptionTranslations_IsObject",
            "jsonb_typeof(\"DescriptionTranslations\") = 'object'"));
    }
}
