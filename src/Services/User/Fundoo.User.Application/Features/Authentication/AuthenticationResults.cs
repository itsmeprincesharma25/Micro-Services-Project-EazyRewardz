namespace Fundoo.User.Application.Features.Authentication;

public sealed record RegisterUserResult(bool Succeeded, Guid? UserId, string? ErrorMessage);
public sealed record LoginUserResult(
    bool Succeeded,
    Guid? UserId,
    string? Email,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAtUtc);
public sealed record ForgotPasswordResult(bool EmailSent);
public sealed record ResetPasswordResult(bool Succeeded, string? ErrorMessage);

public sealed record GeneratedAccessToken(string Value, DateTimeOffset ExpiresAtUtc);
