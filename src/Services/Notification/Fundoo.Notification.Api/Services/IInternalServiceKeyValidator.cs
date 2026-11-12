namespace Fundoo.Notification.Api.Services;

public interface IInternalServiceKeyValidator
{
    bool IsValid(string suppliedKey);
}
