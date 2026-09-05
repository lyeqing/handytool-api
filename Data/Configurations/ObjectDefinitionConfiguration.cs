using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class ObjectDefinitionConfiguration : IEntityTypeConfiguration<ObjectDefinition>
{
    public void Configure(EntityTypeBuilder<ObjectDefinition> builder)
    {
        builder.ToTable("ObjectDefinitions", t =>
        {
            t.HasCheckConstraint("CK_ObjectDefinitions_RequiredAccessLevel", "\"RequiredAccessLevel\" BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_ObjectDefinitions_Visibility", "\"Visibility\" IN (0, 1, 2)");
            t.HasCheckConstraint("CK_ObjectDefinitions_CompanyVisibility",
                """"Visibility" <> 1 OR "CompanyId" IS NOT NULL""");
        });
        builder.Property(x => x.Visibility).HasConversion<int>().HasDefaultValue(DefinitionVisibility.Private);
        builder.Property(x => x.RequiredAccessLevel).HasDefaultValue(0);
        builder.HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.MasterCategoryId).HasDefaultValue(MasterCategory.UncategorizedId);
        builder.HasOne(x => x.MasterCategory).WithMany()
            .HasForeignKey(x => x.MasterCategoryId).OnDelete(DeleteBehavior.Restrict);
        // A composite FK ensures the selected subcategory belongs to the same master category.
        builder.HasOne(x => x.Subcategory).WithMany()
            .HasForeignKey(x => new { x.SubcategoryId, x.MasterCategoryId })
            .HasPrincipalKey(x => new { x.Id, x.MasterCategoryId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.CreatedByUserId).IsRequired();

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

        builder.Property(x => x.CreatedDate).IsRequired();
        builder.Property(x => x.ModifiedDate).IsRequired();

        builder.HasIndex(x => x.CreatedByUserId);

        // Preserve uniqueness of definition names per creator.
        builder.HasIndex(x => new { x.CreatedByUserId, x.Name }).IsUnique();

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
