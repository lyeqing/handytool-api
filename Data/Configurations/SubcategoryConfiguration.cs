using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class SubcategoryConfiguration : IEntityTypeConfiguration<Subcategory>
{
    public void Configure(EntityTypeBuilder<Subcategory> builder)
    {
        builder.ToTable("Subcategories");
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000).HasDefaultValue(string.Empty);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => new { x.MasterCategoryId, x.Name }).IsUnique();
        builder.HasAlternateKey(x => new { x.Id, x.MasterCategoryId });
        builder.HasOne(x => x.MasterCategory).WithMany(x => x.Subcategories)
            .HasForeignKey(x => x.MasterCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
