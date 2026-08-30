using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class TrackingSessionConfiguration : IEntityTypeConfiguration<TrackingSession>
{
    public void Configure(EntityTypeBuilder<TrackingSession> builder)
    {
        builder.ToTable("TrackingSessions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.LastActivityAt).IsRequired();

        // The hot path: "does this client have a session that is still within the idle window?"
        builder.HasIndex(x => new { x.ClientId, x.LastActivityAt });

        builder.HasOne(x => x.Client)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
