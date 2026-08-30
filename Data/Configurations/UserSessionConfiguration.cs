using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ClientType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.UserAgent).HasMaxLength(512);

        builder.Property(x => x.CreatedDate).IsRequired();
        builder.Property(x => x.LastUsedDate).IsRequired();
        builder.Property(x => x.ExpiresDate).IsRequired();

        // Every authenticated request is one lookup on this index, so it carries the load of the
        // whole auth system. Unique because a hash collision here would mean cross-account access.
        builder.HasIndex(x => x.TokenHash).IsUnique();

        // "List my devices" and "log me out everywhere".
        builder.HasIndex(x => new { x.UserId, x.RevokedDate });

        // Cleaning out dead sessions.
        builder.HasIndex(x => x.ExpiresDate);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
