namespace Fundoo.User.Application.Abstractions;

public interface IPasswordResetEmailSender
{
    Task<bool> SendAsync(string email, string resetToken, CancellationToken cancellationToken);
}
