using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class FieldDefinitionTranslationConfiguration : IEntityTypeConfiguration<FieldDefinitionTranslation>
{
    public void Configure(EntityTypeBuilder<FieldDefinitionTranslation> builder)
    {
        builder.ToTable("FieldDefinitionTranslations");
        builder.HasKey(x => new { x.FieldDefinitionId, x.LanguageCode });
        builder.Property(x => x.LanguageCode).HasMaxLength(35);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Placeholder).HasMaxLength(2000);
        builder.HasOne(x => x.FieldDefinition).WithMany(x => x.Translations)
            .HasForeignKey(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}
