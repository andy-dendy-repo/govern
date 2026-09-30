using Govern.Client.Features.Games.Helpers;

namespace Govern.ClientTests.Features.Games.Helpers;

public class InviteLinkTests
{
    private const string Code = "AbCdEfGhIjKlMnOpQrSt_-";

    [Fact]
    public void Build_BaseUriAndCode_PutsTheCodeInTheFragmentOfTheJoinPage()
    {
        InviteLink.Build("https://govern.example/", Code).ShouldBe($"https://govern.example/join#{Code}");
    }

    [Theory]
    [InlineData(Code)]
    [InlineData($"  {Code}  ")]
    [InlineData($"https://govern.example/join#{Code}")]
    [InlineData($"https://govern.example/join/#{Code}")]
    [InlineData($"/join#{Code}")]
    [InlineData($"join#{Code}")]
    [InlineData($" https://govern.example/join#{Code}\n")]
    public void TryParse_CodeOrJoinLink_ReturnsTheCode(string input)
    {
        InviteLink.TryParse(input, out var code).ShouldBeTrue();
        code.ShouldBe(Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AbCdEfGhIjKlMnOpQrSt_")]
    [InlineData("AbCdEfGhIjKlMnOpQrSt_-x")]
    [InlineData("AbCdEfGhIjKlMnOpQrSt+/")]
    [InlineData("AbCdEfGhIjKlMnOpQrSt%2")]
    [InlineData("AbCdEfGhIj KlMnOpQrSt_")]
    [InlineData($"https://govern.example/games#{Code}")]
    [InlineData($"https://govern.example/join?code={Code}")]
    [InlineData($"https://govern.example/join#{Code}#x")]
    [InlineData("https://govern.example/join#")]
    public void TryParse_AnythingElse_ReturnsFalse(string? input)
    {
        InviteLink.TryParse(input, out var code).ShouldBeFalse();
        code.ShouldBeNull();
    }
}
