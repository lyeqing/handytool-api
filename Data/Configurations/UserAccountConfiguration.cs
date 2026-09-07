using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("UserAccounts", t => t.HasCheckConstraint(
            "CK_UserAccounts_CompanyMembership",
            """("CompanyId" IS NULL AND "CompanyRole" IS NULL AND "AccountTypeId" IS NOT NULL) OR ("CompanyId" IS NOT NULL AND "CompanyRole" IS NOT NULL AND "CompanyRole" IN (0, 1, 2) AND "AccountTypeId" IS NULL)"""));

        builder.Property(x => x.CompanyRole).HasConversion<int>();
        builder.Property(x => x.IsSuperAdmin).HasDefaultValue(false);
        // Preserve explicit null for company members; the CLR and SQL defaults serve independent users.
        builder.Property(x => x.AccountTypeId)
            .HasDefaultValue(AccountType.FreeId).ValueGeneratedNever();
        builder.HasOne(x => x.AccountType).WithMany()
            .HasForeignKey(x => x.AccountTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Company).WithMany(x => x.Users)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.PasswordSalt)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.PreferredLanguage).HasMaxLength(10);
        builder.Property(x => x.Phone).HasMaxLength(40);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedDate).IsRequired();
        builder.Property(x => x.ModifiedDate).IsRequired();

        // Email is stored already lowercased, so a plain unique index is enough to stop
        // "Louis@x.com" and "louis@x.com" becoming two accounts.
        builder.HasIndex(x => x.Email).IsUnique();
    }
}
