namespace Fundoo.User.Api.Contracts.Authentication;

public sealed class ResetPasswordRequest
{
    public string Email { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;
}
