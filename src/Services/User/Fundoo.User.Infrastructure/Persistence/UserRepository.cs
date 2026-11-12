using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fundoo.User.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly UserDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public UserRepository(UserDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return _dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return _dbContext.Users.FirstOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    public async Task<bool> AddUserAsync(UserAccount user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsEmailConflict(exception))
        {
            _dbContext.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    public Task AddPasswordResetTokenAsync(
        PasswordResetToken resetToken,
        CancellationToken cancellationToken)
    {
        _dbContext.PasswordResetTokens.Add(resetToken);
        return Task.CompletedTask;
    }

    public Task<PasswordResetToken?> GetPasswordResetTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        return _dbContext.PasswordResetTokens.FirstOrDefaultAsync(
            token => token.TokenHash == tokenHash,
            cancellationToken);
    }

    public async Task InvalidatePasswordResetTokensAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var activeTokens = await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == userId && token.UsedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.UsedAtUtc = _timeProvider.GetUtcNow();
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsEmailConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Users_NormalizedEmail"
        };
    }
}
