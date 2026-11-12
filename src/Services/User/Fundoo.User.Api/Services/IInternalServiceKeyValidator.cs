namespace Fundoo.User.Api.Services;

public interface IInternalServiceKeyValidator
{
    bool IsValid(string? suppliedKey);
}
