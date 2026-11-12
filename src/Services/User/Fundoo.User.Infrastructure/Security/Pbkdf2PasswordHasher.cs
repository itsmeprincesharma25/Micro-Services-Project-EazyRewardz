using System.Security.Cryptography;
using Fundoo.User.Application.Abstractions;

namespace Fundoo.User.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSizeInBytes = 16;
    private const int HashSizeInBytes = 32;
    private const int IterationCount = 600_000;

    public PasswordHashResult Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            HashSizeInBytes);

        return new PasswordHashResult(
            Convert.ToBase64String(hash),
            Convert.ToBase64String(salt),
            IterationCount);
    }

    public bool Verify(string password, string salt, string expectedHash, int iterations)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHashBytes = Convert.FromBase64String(expectedHash);
        var actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            iterations,
            HashAlgorithmName.SHA256,
            HashSizeInBytes);

        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }
}
