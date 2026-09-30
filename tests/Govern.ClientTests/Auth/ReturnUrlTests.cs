using Govern.Client.Auth;

namespace Govern.ClientTests.Auth;

public class ReturnUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/profile")]
    [InlineData("/games?page=2")]
    [InlineData("/games/0199f0a1-0000-7000-8000-000000000000")]
    public void Sanitize_LocalPath_ReturnsIt(string url)
    {
        ReturnUrl.Sanitize(url).ShouldBe(url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("profile")]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/profile\n//evil.example")]
    [InlineData("/pro\tfile")]
    public void Sanitize_NotALocalPath_ReturnsHome(string? url)
    {
        ReturnUrl.Sanitize(url).ShouldBe(ReturnUrl.Home);
    }
}
