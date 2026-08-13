using AgentWire.Application.Security;
using Xunit;

namespace AgentWire.Tests.Security;

public class LuhnValidatorTests
{
    [Theory]
    [InlineData("4111111111111111")] // well-known Visa test number
    [InlineData("5500005555555559")] // well-known Mastercard test number
    [InlineData("340000000000009")]  // well-known Amex test number
    public void KnownValidTestCardNumbers_PassLuhnCheck(string card)
    {
        Assert.True(LuhnValidator.IsValid(card));
    }

    [Theory]
    [InlineData("4111111111111112")] // last digit tampered
    [InlineData("1234567890123456")]
    [InlineData("1111111111111112")]
    public void KnownInvalidNumbers_FailLuhnCheck(string card)
    {
        Assert.False(LuhnValidator.IsValid(card));
    }

    [Fact]
    public void EmptyString_IsInvalid()
    {
        Assert.False(LuhnValidator.IsValid(string.Empty));
    }

    [Fact]
    public void NonDigitCharacters_AreInvalid()
    {
        Assert.False(LuhnValidator.IsValid("411111111111111a"));
    }
}
