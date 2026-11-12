namespace Fundoo.Notes.Application.Abstractions;

public interface IUserDirectoryClient
{
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken);
}
