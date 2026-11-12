namespace Fundoo.Notification.Application.Features;

public sealed record PasswordResetEmailRequest(string Email, string ResetToken);
