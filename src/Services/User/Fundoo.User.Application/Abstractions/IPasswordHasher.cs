namespace Fundoo.User.Application.Abstractions;

public interface IPasswordHasher
{
    PasswordHashResult Hash(string password);
    bool Verify(string password, string salt, string expectedHash, int iterations);
}

public sealed record PasswordHashResult(string Hash, string Salt, int Iterations);
