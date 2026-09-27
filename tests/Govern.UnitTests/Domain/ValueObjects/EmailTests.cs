using Govern.Domain.ValueObjects;

namespace Govern.UnitTests.Domain.ValueObjects;

public class EmailTests
{
    [Fact]
    public void TryCreate_ValidValue_TrimsAndLowerCases()
    {
        var created = Email.TryCreate("  User.Name@Example.COM ", out var email);

        created.ShouldBeTrue();
        email.ShouldNotBeNull();
        email.Value.ShouldBe("user.name@example.com");
    }

    [Fact]
    public void TryCreate_MaxLengthValue_Succeeds()
    {
        var value = $"{new string('a', Email.MaxLength - "@example.com".Length)}@example.com";

        Email.TryCreate(value, out var email).ShouldBeTrue();
        email!.Value.Length.ShouldBe(Email.MaxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("a@b@example.com")]
    public void TryCreate_InvalidValue_ReturnsFalse(string? value)
    {
        var created = Email.TryCreate(value, out var email);

        created.ShouldBeFalse();
        email.ShouldBeNull();
        Email.IsValid(value).ShouldBeFalse();
    }

    [Fact]
    public void TryCreate_LongerThanMaxLength_ReturnsFalse()
    {
        var value = $"{new string('a', Email.MaxLength - "@example.com".Length + 1)}@example.com";

        Email.TryCreate(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void Create_InvalidValue_Throws()
    {
        Should.Throw<ArgumentException>(() => Email.Create("not-an-email"));
    }

    [Fact]
    public void Equality_IgnoresCaseAndSurroundingWhitespace()
    {
        var first = Email.Create("User@Example.com");
        var second = Email.Create(" user@example.com ");

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        Email.Create("User@Example.com").ToString().ShouldBe("user@example.com");
    }
}
