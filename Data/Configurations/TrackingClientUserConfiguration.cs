using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class TrackingClientUserConfiguration : IEntityTypeConfiguration<TrackingClientUser>
{
    public void Configure(EntityTypeBuilder<TrackingClientUser> builder)
    {
        builder.ToTable("TrackingClientUsers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.FirstIdentifiedAt).IsRequired();
        builder.Property(x => x.LastIdentifiedAt).IsRequired();

        // One row per pairing, ever. This is what makes "link on first authenticated sighting" a
        // no-op on every subsequent request instead of a growing pile of duplicates.
        builder.HasIndex(x => new { x.ClientId, x.UserId }).IsUnique();

        // "Which browsers and devices belong to this user?" - step one of the history query.
        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.Client)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
