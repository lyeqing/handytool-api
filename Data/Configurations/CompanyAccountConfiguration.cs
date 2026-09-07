using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class CompanyAccountConfiguration : IEntityTypeConfiguration<CompanyAccount>
{
    public void Configure(EntityTypeBuilder<CompanyAccount> builder)
    {
        builder.ToTable("CompanyAccounts", t => t.HasCheckConstraint(
            "CK_CompanyAccounts_SeatLimit", "\"SeatLimit\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(2000);
        builder.Property(x => x.WebsiteUrl).HasMaxLength(2048);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        // Zero remains an explicit valid limit; one is the default.
        builder.Property(x => x.SeatLimit).HasDefaultValue(1).ValueGeneratedNever();
        builder.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.AccountTypeId).IsRequired()
            .HasDefaultValue(AccountType.FreeId);
        builder.HasOne(x => x.AccountType).WithMany()
            .HasForeignKey(x => x.AccountTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
