using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class SubcategoryTranslationConfiguration : IEntityTypeConfiguration<SubcategoryTranslation>
{
    public void Configure(EntityTypeBuilder<SubcategoryTranslation> builder)
    {
        builder.ToTable("SubcategoryTranslations");
        builder.HasKey(x => new { x.SubcategoryId, x.LanguageCode });
        builder.Property(x => x.LanguageCode).HasMaxLength(35);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasOne(x => x.Subcategory).WithMany(x => x.Translations)
            .HasForeignKey(x => x.SubcategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}
