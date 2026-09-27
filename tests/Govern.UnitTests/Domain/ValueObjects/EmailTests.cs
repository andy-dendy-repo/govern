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
    [InlineData("user\r\n@example.com")]
    [InlineData("us er@example.com")]
    [InlineData("user@exa\tmple.com")]
    [InlineData("user\u200B@example.com")]
    [InlineData("user\u0000@example.com")]
    [InlineData("user\u00A0name@example.com")]
    [InlineData("user\uE000@example.com")]
    [InlineData("user\u034F@example.com")]
    [InlineData("user\u3164@example.com")]
    [InlineData("user\uFE0F@example.com")]
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
    public void TryCreate_LoneSurrogate_ReturnsFalse()
    {
        // Built at runtime: attribute arguments are stored as UTF-8, which replaces a lone surrogate with U+FFFD.
        var value = $"user{(char)0xD800}@example.com";

        Email.TryCreate(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryCreate_DecomposedValue_StoresComposedForm()
    {
        var decomposed = Email.Create("E\u0301cole@example.com");
        var composed = Email.Create("\u00C9cole@example.com");

        decomposed.Value.ShouldBe("\u00E9cole@example.com");
        decomposed.ShouldBe(composed);
    }

    [Theory]
    [InlineData("J\u030C@example.com", "\u01F0@example.com")]
    [InlineData("T\u0308@example.com", "\u1E97@example.com")]
    [InlineData("\u03AA\u0301@example.com", "\u0390@example.com")]
    public void TryCreate_CaseVariantWithoutPrecomposedCapital_EqualsPrecomposedLowerCase(string upper, string lower)
    {
        var fromUpper = Email.Create(upper);

        fromUpper.ShouldBe(Email.Create(lower));
        Email.Create(fromUpper.Value).ShouldBe(fromUpper);
        fromUpper.Value.IsNormalized(System.Text.NormalizationForm.FormC).ShouldBeTrue();
    }

    [Fact]
    public void TryCreate_LongerThanMaxLengthOnlyBeforeNormalisation_Succeeds()
    {
        // Domain rule only: the Api validators also cap the raw input at MaxLength before this runs.
        var value = $"{new string('a', Email.MaxLength - "@example.com".Length - 1)}e\u0301@example.com";

        Email.TryCreate(value, out var email).ShouldBeTrue();
        email.Value.Length.ShouldBe(Email.MaxLength);
    }

    [Fact]
    public void TryCreate_NonAsciiLocalPart_Succeeds()
    {
        Email.TryCreate("\u0434\u0430\u043D\u0438\u043B\u043E@example.com", out _).ShouldBeTrue();
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
