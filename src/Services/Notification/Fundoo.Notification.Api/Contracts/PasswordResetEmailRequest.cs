namespace Fundoo.Notification.Api.Contracts;

public sealed class PasswordResetEmailRequest
{
    public string Email { get; init; } = string.Empty;

    public string ResetToken { get; init; } = string.Empty;
}
