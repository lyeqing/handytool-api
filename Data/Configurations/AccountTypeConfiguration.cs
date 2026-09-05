using handytool_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace handytool_api.Data.Configurations;

public class AccountTypeConfiguration : IEntityTypeConfiguration<AccountType>
{
    public void Configure(EntityTypeBuilder<AccountType> builder)
    {
        builder.ToTable("AccountTypes", t => t.HasCheckConstraint(
            "CK_AccountTypes_AccessLevel", "\"AccessLevel\" BETWEEN 1 AND 3"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 4);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasData(
            new AccountType { Id = AccountType.FreeId, Code = "free", Name = "Free", AccessLevel = 1 },
            new AccountType { Id = AccountType.LightId, Code = "light", Name = "Light", AccessLevel = 2 },
            new AccountType { Id = AccountType.FullId, Code = "full", Name = "Full", AccessLevel = 3 });
    }
}
