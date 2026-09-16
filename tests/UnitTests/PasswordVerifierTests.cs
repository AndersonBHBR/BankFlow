using BankFlow.Identity.Api.Auth;
using Xunit;

namespace BankFlow.UnitTests;

public sealed class PasswordVerifierTests
{
    private const string Salt = "YLbLvyIQuJVrZ/rKhhA+aQ==";
    private const string Hash = "TZWCjSs6I/n0CLEbJuOQTTXgYTL+WzgQ/Tk2oDpS1Ac=";

    private readonly Pbkdf2PasswordVerifier _sut = new();

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var result = _sut.Verify("BankFlow#2026", Salt, Hash);

        Assert.True(result);
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        var result = _sut.Verify("senha-incorreta", Salt, Hash);

        Assert.False(result);
    }
}
