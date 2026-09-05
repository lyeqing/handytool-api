using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class ObjectDefinitionTranslationConfiguration : IEntityTypeConfiguration<ObjectDefinitionTranslation>
{
    public void Configure(EntityTypeBuilder<ObjectDefinitionTranslation> builder)
    {
        builder.ToTable("ObjectDefinitionTranslations");
        builder.HasKey(x => new { x.ObjectDefinitionId, x.LanguageCode });
        builder.Property(x => x.LanguageCode).HasMaxLength(35);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasOne(x => x.ObjectDefinition).WithMany(x => x.Translations)
            .HasForeignKey(x => x.ObjectDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}
