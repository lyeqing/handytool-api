using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class FieldOptionTranslationConfiguration : IEntityTypeConfiguration<FieldOptionTranslation>
{
    public void Configure(EntityTypeBuilder<FieldOptionTranslation> builder)
    {
        builder.ToTable("FieldOptionTranslations");
        builder.HasKey(x => new { x.FieldOptionId, x.LanguageCode });
        builder.Property(x => x.LanguageCode).HasMaxLength(35);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.HasOne(x => x.FieldOption).WithMany(x => x.Translations)
            .HasForeignKey(x => x.FieldOptionId).OnDelete(DeleteBehavior.Cascade);
    }
}
