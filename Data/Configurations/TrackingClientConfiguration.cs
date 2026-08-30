using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class TrackingClientConfiguration : IEntityTypeConfiguration<TrackingClient>
{
    public void Configure(EntityTypeBuilder<TrackingClient> builder)
    {
        builder.ToTable("TrackingClients");

        // The key is supplied by the application, not the database: for the website it is the GUID
        // that was written into the visitor cookie.
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ClientType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.FirstSeenDate).IsRequired();
        builder.Property(x => x.LastSeenDate).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired();

        builder.HasIndex(x => x.ClientType);
        builder.HasIndex(x => x.LastSeenDate);
    }
}
