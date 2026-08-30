using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class ObjectRecordConfiguration : IEntityTypeConfiguration<ObjectRecord>
{
    public void Configure(EntityTypeBuilder<ObjectRecord> builder)
    {
        builder.ToTable("ObjectRecords");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(4000)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.Values)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(x => x.CreatedDate).IsRequired();
        builder.Property(x => x.ModifiedDate).IsRequired();

        builder.HasIndex(x => x.ObjectDefinitionId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedDate);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ObjectDefinition)
            .WithMany(x => x.Records)
            .HasForeignKey(x => x.ObjectDefinitionId)
            // User data must never disappear because metadata was deleted.
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ObjectRecords_Values_IsObject",
            "jsonb_typeof(\"Values\") = 'object'"));
    }
}
