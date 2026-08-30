using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.ToTable("AnalyticsEvents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.ClientType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Timestamp).IsRequired();

        builder.Property(x => x.Language).HasMaxLength(10);

        builder.Property(x => x.Referrer).HasMaxLength(1000);
        builder.Property(x => x.UserAgent).HasMaxLength(512);

        // Both halves of the history query in one index each: events recorded against the user
        // directly, and events recorded against one of that user's clients while anonymous.
        builder.HasIndex(x => new { x.UserId, x.Timestamp });
        builder.HasIndex(x => new { x.ClientId, x.Timestamp });

        builder.HasIndex(x => new { x.SessionId, x.Timestamp });
        builder.HasIndex(x => x.EventType);

        // "Which language do visitors actually read the site in?" - the question this column exists
        // to answer, and one that gets asked across the whole table rather than per visitor.
        builder.HasIndex(x => x.Language);
        builder.HasIndex(x => x.Timestamp);

        // Analytics history outlives the identities it references: deleting a visitor or a session
        // must never silently rewrite what happened.
        builder.HasOne(x => x.Client)
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AnalyticsEvents_ActiveSeconds_NonNegative",
            "\"ActiveSeconds\" IS NULL OR \"ActiveSeconds\" >= 0"));
    }
}
