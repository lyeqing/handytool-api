using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class MasterCategoryConfiguration : IEntityTypeConfiguration<MasterCategory>
{
    public void Configure(EntityTypeBuilder<MasterCategory> builder)
    {
        builder.ToTable("MasterCategories");
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000).HasDefaultValue(string.Empty);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasData(new MasterCategory
        {
            Id = MasterCategory.UncategorizedId,
            Name = "Uncategorized",
            Description = "Default category for definitions awaiting classification.",
            CreatedDate = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc),
            ModifiedDate = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
