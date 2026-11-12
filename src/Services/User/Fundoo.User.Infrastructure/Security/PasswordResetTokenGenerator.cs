using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Fundoo.User.Application.Abstractions;

namespace Fundoo.User.Infrastructure.Security;

public sealed class PasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    public string CreateToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(tokenBytes);
    }

    public string HashToken(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes);
    }
}
