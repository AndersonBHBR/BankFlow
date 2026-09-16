using System.Security.Cryptography;

namespace BankFlow.Identity.Api.Auth;

public interface IPasswordVerifier
{
    public bool Verify(string password, string saltBase64, string expectedHashBase64);
}

public sealed class Pbkdf2PasswordVerifier : IPasswordVerifier
{
    private const int Iterations = 100_000;
    private const int HashLength = 32;

    public bool Verify(string password, string saltBase64, string expectedHashBase64)
    {
        var salt = Convert.FromBase64String(saltBase64);
        var expectedHash = Convert.FromBase64String(expectedHashBase64);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashLength);

        return expectedHash.Length == actualHash.Length &&
               CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
