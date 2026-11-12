using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Features.Authentication;

public sealed class UserAccountService
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(15);
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenGenerator _accessTokenGenerator;
    private readonly IPasswordResetTokenGenerator _resetTokenGenerator;
    private readonly IPasswordResetEmailSender _passwordResetEmailSender;
    private readonly TimeProvider _timeProvider;

    public UserAccountService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAccessTokenGenerator accessTokenGenerator,
        IPasswordResetTokenGenerator resetTokenGenerator,
        IPasswordResetEmailSender passwordResetEmailSender,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _accessTokenGenerator = accessTokenGenerator;
        _resetTokenGenerator = resetTokenGenerator;
        _passwordResetEmailSender = passwordResetEmailSender;
        _timeProvider = timeProvider;
    }

    public async Task<RegisterUserResult> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var cleanedEmail = email.Trim();
        var normalizedEmail = NormalizeEmail(cleanedEmail);
        var emailExists = await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return new RegisterUserResult(
                false,
                null,
                "An account with this email already exists.");
        }

        var passwordHash = _passwordHasher.Hash(password);
        var user = new UserAccount
        {
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = cleanedEmail,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash.Hash,
            PasswordSalt = passwordHash.Salt,
            PasswordHashIterations = passwordHash.Iterations,
            CreatedAtUtc = _timeProvider.GetUtcNow()
        };

        var userWasAdded = await _userRepository.AddUserAsync(user, cancellationToken);
        if (!userWasAdded)
        {
            return new RegisterUserResult(
                false,
                null,
                "An account with this email already exists.");
        }

        return new RegisterUserResult(true, user.Id, null);
    }

    public async Task<LoginUserResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);
        if (user is null)
        {
            return new LoginUserResult(false, null, null, null, null);
        }

        var passwordIsValid = _passwordHasher.Verify(
            password, user.PasswordSalt, user.PasswordHash, user.PasswordHashIterations);
        if (!passwordIsValid)
        {
            return new LoginUserResult(false, null, null, null, null);
        }

        var accessToken = _accessTokenGenerator.Create(user.Id, user.Email);
        return new LoginUserResult(
            true,
            user.Id,
            user.Email,
            accessToken.Value,
            accessToken.ExpiresAtUtc);
    }

    public async Task<Guid?> FindUserIdByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);
        return user?.Id;
    }

    public async Task<ForgotPasswordResult> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);
        if (user is null)
        {
            return new ForgotPasswordResult(false);
        }

        await _userRepository.InvalidatePasswordResetTokensAsync(user.Id, cancellationToken);
        var resetToken = _resetTokenGenerator.CreateToken();
        var resetTokenEntity = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = _resetTokenGenerator.HashToken(resetToken),
            ExpiresAtUtc = _timeProvider.GetUtcNow().Add(ResetTokenLifetime)
        };

        await _userRepository.AddPasswordResetTokenAsync(resetTokenEntity, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        var emailWasSent = await _passwordResetEmailSender.SendAsync(
            user.Email,
            resetToken,
            cancellationToken);

        if (!emailWasSent)
        {
            await _userRepository.InvalidatePasswordResetTokensAsync(user.Id, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        return new ForgotPasswordResult(emailWasSent);
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);
        if (user is null)
        {
            return InvalidResetRequest();
        }

        var tokenHash = _resetTokenGenerator.HashToken(token);
        var resetToken = await _userRepository.GetPasswordResetTokenAsync(tokenHash, cancellationToken);
        if (resetToken is null)
        {
            return InvalidResetRequest();
        }

        if (resetToken.UserId != user.Id)
        {
            return InvalidResetRequest();
        }

        var tokenWasAlreadyUsed = resetToken.UsedAtUtc.HasValue;
        if (tokenWasAlreadyUsed)
        {
            return InvalidResetRequest();
        }

        var tokenHasExpired = resetToken.ExpiresAtUtc <= _timeProvider.GetUtcNow();
        if (tokenHasExpired)
        {
            return InvalidResetRequest();
        }

        var passwordHash = _passwordHasher.Hash(newPassword);
        user.PasswordHash = passwordHash.Hash;
        user.PasswordSalt = passwordHash.Salt;
        user.PasswordHashIterations = passwordHash.Iterations;
        resetToken.UsedAtUtc = _timeProvider.GetUtcNow();
        await _userRepository.InvalidatePasswordResetTokensAsync(user.Id, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
        return new ResetPasswordResult(true, null);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static ResetPasswordResult InvalidResetRequest() =>
        new ResetPasswordResult(false, "The reset request is invalid or has expired.");
}
