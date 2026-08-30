using handytool_api.Security;

namespace handytool_api.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Correct_password_verifies()
    {
        var (hash, salt) = PasswordHasher.Hash("correct horse battery staple");

        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash, salt));
    }

    [Fact]
    public void Wrong_password_does_not_verify()
    {
        var (hash, salt) = PasswordHasher.Hash("correct horse battery staple");

        Assert.False(PasswordHasher.Verify("Correct horse battery staple", hash, salt));
        Assert.False(PasswordHasher.Verify(string.Empty, hash, salt));
    }

    [Fact]
    public void Two_accounts_with_the_same_password_get_different_hashes()
    {
        var first = PasswordHasher.Hash("same password");
        var second = PasswordHasher.Hash("same password");

        // Per-account salts. A single shared salt would let one rainbow table cover every user, and
        // would make identical passwords visible to anyone reading the table.
        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void A_hash_from_another_account_does_not_verify()
    {
        var (hash, _) = PasswordHasher.Hash("shared password");
        var (_, otherSalt) = PasswordHasher.Hash("shared password");

        Assert.False(PasswordHasher.Verify("shared password", hash, otherSalt));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("not base64!", "not base64!")]
    public void Unusable_stored_credentials_never_verify(string hash, string salt)
    {
        // The migration gives adopted legacy owners an empty hash precisely so that nothing can sign
        // in as them. That has to fail closed rather than throw.
        Assert.False(PasswordHasher.Verify("anything", hash, salt));
    }
}
