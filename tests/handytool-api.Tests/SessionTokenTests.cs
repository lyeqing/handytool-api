using handytool_api.Models;
using handytool_api.Security;

namespace handytool_api.Tests;

public class SessionTokenTests
{
    [Fact]
    public void Every_token_is_different()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => SessionToken.Create()).ToHashSet();

        Assert.Equal(200, tokens.Count);
    }

    [Fact]
    public void Tokens_are_url_safe()
    {
        // They travel in an Authorization header and may be stored by a native client; the two
        // base64 characters that need escaping are not worth the trouble.
        var token = SessionToken.Create();

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
        Assert.True(token.Length >= 40);
    }

    [Fact]
    public void Hashing_is_stable_so_the_same_token_finds_the_same_session()
    {
        var token = SessionToken.Create();

        Assert.Equal(SessionToken.Hash(token), SessionToken.Hash(token));
    }

    [Fact]
    public void The_stored_hash_is_not_the_token()
    {
        var token = SessionToken.Create();

        // What is stored must not be usable as a credential if the table is ever read.
        Assert.NotEqual(token, SessionToken.Hash(token));
    }

    [Fact]
    public void Different_tokens_hash_differently()
    {
        Assert.NotEqual(SessionToken.Hash(SessionToken.Create()), SessionToken.Hash(SessionToken.Create()));
    }
}

public class UserSessionLifetimeTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

    private static UserSession Session(DateTime expires, DateTime? revoked = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = 1,
        TokenHash = "hash",
        CreatedDate = Now.AddDays(-1),
        LastUsedDate = Now.AddDays(-1),
        ExpiresDate = expires,
        RevokedDate = revoked
    };

    [Fact]
    public void A_live_session_is_usable()
    {
        Assert.True(Session(Now.AddDays(30)).IsActiveAt(Now));
    }

    [Fact]
    public void An_expired_session_is_not_usable()
    {
        Assert.False(Session(Now.AddSeconds(-1)).IsActiveAt(Now));
    }

    [Fact]
    public void A_revoked_session_is_not_usable_even_before_it_expires()
    {
        // This is the whole reason for database-backed tokens: signing out has to take effect now,
        // not whenever the credential would have run out on its own.
        Assert.False(Session(Now.AddDays(30), revoked: Now.AddMinutes(-1)).IsActiveAt(Now));
    }
}
