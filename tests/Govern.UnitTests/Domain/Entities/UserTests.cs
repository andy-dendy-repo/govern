using Govern.Domain.Entities;
using Govern.Domain.ValueObjects;

namespace Govern.UnitTests.Domain.Entities;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    [Fact]
    public void IssueRefreshToken_BelowCap_AddsTokenWithoutRevoking()
    {
        var user = NewUser();
        var first = user.IssueRefreshToken("hash-1", Now + Lifetime, Now, maxActiveTokens: 2);

        var second = user.IssueRefreshToken("hash-2", Now.AddMinutes(1) + Lifetime, Now.AddMinutes(1), maxActiveTokens: 2);

        user.RefreshTokens.Count.ShouldBe(2);
        first.RevokedAt.ShouldBeNull();
        second.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void IssueRefreshToken_AtCap_RevokesEarliestExpiringTokenWithoutSuccessor()
    {
        var user = NewUser();
        var later = user.IssueRefreshToken("hash-later", Now.AddMinutes(5) + Lifetime, Now, maxActiveTokens: 2);
        var oldest = user.IssueRefreshToken("hash-oldest", Now + Lifetime, Now, maxActiveTokens: 2);
        var signInAt = Now.AddMinutes(10);

        var issued = user.IssueRefreshToken("hash-new", signInAt + Lifetime, signInAt, maxActiveTokens: 2);

        oldest.RevokedAt.ShouldBe(signInAt);
        oldest.ReplacedByTokenId.ShouldBeNull();
        later.IsActive(signInAt).ShouldBeTrue();
        issued.IsActive(signInAt).ShouldBeTrue();
    }

    [Fact]
    public void IssueRefreshToken_ExpiredAndRevokedTokens_DoNotCountTowardCap()
    {
        var user = NewUser();
        var expired = user.IssueRefreshToken("hash-expired", Now.AddMinutes(1), Now, maxActiveTokens: 2);
        var rotated = user.IssueRefreshToken("hash-rotated", Now + Lifetime, Now, maxActiveTokens: 2);
        var active = user.RotateRefreshToken(rotated, "hash-active", Now + Lifetime, Now);
        var signInAt = Now.AddHours(1);

        user.IssueRefreshToken("hash-new", signInAt + Lifetime, signInAt, maxActiveTokens: 2);

        active.IsActive(signInAt).ShouldBeTrue();
        expired.RevokedAt.ShouldBeNull();
        user.RefreshTokens.Count(token => token.IsActive(signInAt)).ShouldBe(2);
    }

    [Fact]
    public void IssueRefreshToken_AboveCap_RevokesEnoughToFitCap()
    {
        var user = NewUser();
        var tokens = Enumerable.Range(0, 4)
            .Select(i => user.IssueRefreshToken($"hash-{i}", Now.AddMinutes(i) + Lifetime, Now, maxActiveTokens: 10))
            .ToList();
        var signInAt = Now.AddHours(1);

        var issued = user.IssueRefreshToken("hash-new", signInAt + Lifetime, signInAt, maxActiveTokens: 2);

        tokens.Take(3).ShouldAllBe(token => token.RevokedAt == signInAt);
        tokens[3].IsActive(signInAt).ShouldBeTrue();
        issued.IsActive(signInAt).ShouldBeTrue();
        user.RefreshTokens.Count(token => token.IsActive(signInAt)).ShouldBe(2);
    }

    [Fact]
    public void IssueRefreshToken_CapBelowOne_Throws()
    {
        var user = NewUser();

        Should.Throw<ArgumentOutOfRangeException>(() => user.IssueRefreshToken("hash", Now + Lifetime, Now, maxActiveTokens: 0));
    }

    [Fact]
    public void RotateRefreshToken_AtCap_DoesNotRevokeOtherSessions()
    {
        var user = NewUser();
        var other = user.IssueRefreshToken("hash-other", Now + Lifetime, Now, maxActiveTokens: 2);
        var current = user.IssueRefreshToken("hash-current", Now.AddMinutes(1) + Lifetime, Now, maxActiveTokens: 2);

        var replacement = user.RotateRefreshToken(current, "hash-replacement", Now.AddMinutes(2) + Lifetime, Now.AddMinutes(2));

        other.IsActive(Now.AddMinutes(2)).ShouldBeTrue();
        current.ReplacedByTokenId.ShouldBe(replacement.Id);
    }

    [Fact]
    public void RevokeRefreshToken_OwnToken_RevokesWithoutSuccessorAndIsIdempotent()
    {
        var user = NewUser();
        var token = user.IssueRefreshToken("hash", Now + Lifetime, Now, maxActiveTokens: 2);

        user.RevokeRefreshToken(token, Now.AddMinutes(1));
        user.RevokeRefreshToken(token, Now.AddMinutes(2));

        token.RevokedAt.ShouldBe(Now.AddMinutes(1));
        token.ReplacedByTokenId.ShouldBeNull();
    }

    [Fact]
    public void RevokeRefreshToken_AnotherUsersToken_Throws()
    {
        var user = NewUser();
        var othersToken = NewUser().IssueRefreshToken("hash", Now + Lifetime, Now, maxActiveTokens: 2);

        Should.Throw<InvalidOperationException>(() => user.RevokeRefreshToken(othersToken, Now));
    }

    [Fact]
    public void RevokeAllRefreshTokens_RevokesOnlyActiveTokens()
    {
        var user = NewUser();
        var expired = user.IssueRefreshToken("hash-expired", Now.AddMinutes(1), Now, maxActiveTokens: 5);
        var first = user.IssueRefreshToken("hash-1", Now + Lifetime, Now, maxActiveTokens: 5);
        var second = user.IssueRefreshToken("hash-2", Now + Lifetime, Now, maxActiveTokens: 5);
        var revokeAt = Now.AddHours(1);

        user.RevokeAllRefreshTokens(revokeAt);

        first.RevokedAt.ShouldBe(revokeAt);
        second.RevokedAt.ShouldBe(revokeAt);
        expired.RevokedAt.ShouldBeNull();
    }

    private static User NewUser() => User.SignUp(Email.Create($"user-{Guid.NewGuid():N}@example.com"), "password-hash");
}
