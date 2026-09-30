using System.Buffers.Text;
using System.Text;
using Govern.Client.Auth;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Auth;

public class JwtPayloadTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryRead_ApiShapedToken_ReadsClaimsAndLifetime()
    {
        var userId = Guid.CreateVersion7();

        var payload = JwtPayload.TryRead(TestTokens.AccessToken("player@example.com", IssuedAt, userId: userId));

        payload.ShouldNotBeNull();
        payload.IssuedAt.ShouldBe(IssuedAt);
        payload.Lifetime.ShouldBe(TestTokens.AccessTokenLifetime);
        payload.Claims.ShouldContain(claim => claim.Type == "email" && claim.Value == "player@example.com");
        payload.Claims.ShouldContain(claim => claim.Type == "sub" && claim.Value == userId.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("only.two")]
    [InlineData("a.b.c.d")]
    [InlineData("header.!!!.signature")]
    public void TryRead_NotAJwt_ReturnsNull(string? token)
    {
        JwtPayload.TryRead(token).ShouldBeNull();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"iat\":1}")]
    [InlineData("{\"exp\":1}")]
    [InlineData("{\"iat\":\"x\",\"exp\":1}")]
    [InlineData("{\"iat\":1,\"exp\":99999999999999999}")]
    public void TryRead_PayloadWithoutValidTimes_ReturnsNull(string json)
    {
        var token = $"header.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json))}.signature";

        JwtPayload.TryRead(token).ShouldBeNull();
    }
}
