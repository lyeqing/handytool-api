using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class MasterCategoryTranslationConfiguration : IEntityTypeConfiguration<MasterCategoryTranslation>
{
    public void Configure(EntityTypeBuilder<MasterCategoryTranslation> builder)
    {
        builder.ToTable("MasterCategoryTranslations");
        builder.HasKey(x => new { x.MasterCategoryId, x.LanguageCode });
        builder.Property(x => x.LanguageCode).HasMaxLength(35);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasOne(x => x.MasterCategory).WithMany(x => x.Translations)
            .HasForeignKey(x => x.MasterCategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}
