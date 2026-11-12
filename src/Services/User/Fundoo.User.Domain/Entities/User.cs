namespace Fundoo.User.Domain.Entities;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }
    public int PasswordHashIterations { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
