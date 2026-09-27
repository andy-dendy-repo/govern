using Govern.Domain.ValueObjects;

namespace Govern.UnitTests.Domain.ValueObjects;

public class InviteCodeTests
{
    [Fact]
    public void New_Returns22Base64UrlCharacters()
    {
        var code = InviteCode.New();

        code.Value.Length.ShouldBe(InviteCode.MaxLength);
        code.Value.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
    }

    [Fact]
    public void New_ThousandCalls_AreAllDistinct()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => InviteCode.New().Value).ToHashSet(StringComparer.Ordinal);

        codes.Count.ShouldBe(1000);
    }

    [Fact]
    public void Create_ValueOfNewCode_RoundTrips()
    {
        var code = InviteCode.New();

        var parsed = InviteCode.Create(code.Value);

        parsed.ShouldBe(code);
        parsed.Value.ShouldBe(code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAA+/")]
    [InlineData("AAAAAAAAAAAAAAAAAAAA==")]
    [InlineData(" AAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA\n")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAé")]
    public void TryCreate_Malformed_ReturnsFalse(string? value)
    {
        InviteCode.TryCreate(value, out var code).ShouldBeFalse();
        code.ShouldBeNull();
        InviteCode.IsValid(value).ShouldBeFalse();
    }

    [Fact]
    public void Create_DiffersOnlyInCase_IsADifferentCode()
    {
        var lower = InviteCode.Create("abcdefghijklmnopqrstuv");

        var upper = InviteCode.Create("ABCDEFGHIJKLMNOPQRSTUV");

        upper.ShouldNotBe(lower);
    }

    [Fact]
    public void Create_Malformed_Throws()
    {
        Should.Throw<ArgumentException>(() => InviteCode.Create("short"));
    }
}
