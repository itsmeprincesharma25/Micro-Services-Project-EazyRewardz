using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.Features.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Fundoo.User.Infrastructure.Security;

public sealed class JwtAccessTokenGenerator : IAccessTokenGenerator
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _signingKey;
    private readonly int _tokenLifetimeMinutes;
    private readonly TimeProvider _timeProvider;

    public JwtAccessTokenGenerator(IConfiguration configuration, TimeProvider timeProvider)
    {
        _issuer = configuration["Jwt:Issuer"] ?? string.Empty;
        _audience = configuration["Jwt:Audience"] ?? string.Empty;
        _signingKey = configuration["Jwt:SigningKey"] ?? string.Empty;
        _tokenLifetimeMinutes = configuration.GetValue<int>("Jwt:AccessTokenMinutes");
        _timeProvider = timeProvider;

        if (string.IsNullOrWhiteSpace(_issuer))
        {
            throw new InvalidOperationException("JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_audience))
        {
            throw new InvalidOperationException("JWT audience is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(_signingKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be configured and contain at least 32 bytes.");
        }

        if (_tokenLifetimeMinutes < 1)
        {
            throw new InvalidOperationException(
                "JWT access token lifetime must be at least one minute.");
        }
    }

    public GeneratedAccessToken Create(Guid userId, string email)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(_tokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: signingCredentials);

        return new GeneratedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}
