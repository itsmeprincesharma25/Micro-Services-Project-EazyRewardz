namespace Fundoo.Notes.Application.Abstractions;

public sealed class UserDirectoryUnavailableException : Exception
{
    public UserDirectoryUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
