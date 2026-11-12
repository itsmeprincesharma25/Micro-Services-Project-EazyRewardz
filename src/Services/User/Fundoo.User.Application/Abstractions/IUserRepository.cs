using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Abstractions;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<bool> AddUserAsync(UserAccount user, CancellationToken cancellationToken);
    Task AddPasswordResetTokenAsync(PasswordResetToken resetToken, CancellationToken cancellationToken);
    Task<PasswordResetToken?> GetPasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task InvalidatePasswordResetTokensAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
