namespace Fundoo.User.Application.Abstractions;

public interface IPasswordResetTokenGenerator
{
    string CreateToken();
    string HashToken(string token);
}
