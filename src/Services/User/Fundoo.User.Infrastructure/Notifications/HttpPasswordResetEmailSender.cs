using System.Net.Http.Json;
using Fundoo.User.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fundoo.User.Infrastructure.Notifications;

public sealed class HttpPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPasswordResetEmailSender> _logger;

    public HttpPasswordResetEmailSender(
        HttpClient httpClient,
        ILogger<HttpPasswordResetEmailSender> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken)
    {
        var request = new PasswordResetEmailRequest(email, resetToken);
        var idempotencyKey = Guid.NewGuid();

        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                "api/internal/notifications/password-reset")
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Add("Idempotency-Key", idempotencyKey.ToString("D"));

            using var response = await _httpClient.SendAsync(
                message,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning(
                "Notification service rejected the email request with status code {StatusCode}.",
                (int)response.StatusCode);
            return false;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                "Could not reach the notification service. Failure type: {FailureType}",
                exception.GetType().Name);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("The notification service request timed out.");
            return false;
        }
    }

    private sealed record PasswordResetEmailRequest(string Email, string ResetToken);
}
