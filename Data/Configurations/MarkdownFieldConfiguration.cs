using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class MarkdownFieldConfiguration : IEntityTypeConfiguration<MarkdownField>
{
    public void Configure(EntityTypeBuilder<MarkdownField> builder)
    {
        builder.ToTable("MarkdownFields", t => t.HasCheckConstraint("CK_MarkdownFields_Settings", """("MinimumLength" IS NULL OR "MaximumLength" IS NULL OR "MinimumLength" <= "MaximumLength") AND ("MinimumLength" IS NULL OR "MinimumLength" >= 0) AND ("MaximumLength" IS NULL OR "MaximumLength" >= 0)"""));
        builder.HasKey(x => x.FieldDefinitionId);
        builder.Property(x => x.FieldDefinitionId).ValueGeneratedNever();
        builder.HasOne(x => x.FieldDefinition).WithOne(x => x.MarkdownField)
            .HasForeignKey<MarkdownField>(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Placeholder).HasMaxLength(2000);
    }
}
