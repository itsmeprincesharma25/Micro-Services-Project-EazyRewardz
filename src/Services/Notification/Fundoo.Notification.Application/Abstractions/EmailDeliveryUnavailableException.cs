namespace Fundoo.Notification.Application.Abstractions;

public sealed class EmailDeliveryUnavailableException : Exception
{
    public EmailDeliveryUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
