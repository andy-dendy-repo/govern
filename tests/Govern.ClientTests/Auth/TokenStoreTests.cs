using Govern.Client.Auth;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Auth;

public class TokenStoreTests : ClientTestContext
{
    private TokenStore Store => Services.GetRequiredService<TokenStore>();

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsTheSameTokens()
    {
        var tokens = new StoredTokens("access", "refresh", Time.GetUtcNow().AddDays(14), Time.GetUtcNow().AddMinutes(14));

        await Store.SetAsync(tokens, Ct);

        (await Store.GetAsync(Ct)).ShouldBe(tokens);
        Storage[TokenStore.Key].ShouldNotBeNull().ShouldContain("\"refreshToken\":\"refresh\"");
    }

    [Fact]
    public async Task GetAsync_NothingStored_ReturnsNull()
    {
        (await Store.GetAsync(Ct)).ShouldBeNull();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"accessToken\":\"\",\"refreshToken\":\"refresh\"}")]
    [InlineData("{\"accessToken\":\"access\"}")]
    public async Task GetAsync_MalformedEntry_RemovesItAndReturnsNull(string json)
    {
        Storage[TokenStore.Key] = json;

        (await Store.GetAsync(Ct)).ShouldBeNull();

        Storage[TokenStore.Key].ShouldBeNull();
    }

    [Fact]
    public async Task ClearAsync_Stored_RemovesTheEntry()
    {
        SignIn();

        await Store.ClearAsync(Ct);

        Storage[TokenStore.Key].ShouldBeNull();
    }
}
