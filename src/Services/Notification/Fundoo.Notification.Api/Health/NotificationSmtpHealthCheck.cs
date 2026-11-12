using System.Net.Sockets;
using Fundoo.Notification.Infrastructure.Email;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Fundoo.Notification.Api.Health;

public sealed class NotificationSmtpHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(3);
    private readonly IOptions<SmtpOptions> _options;

    public NotificationSmtpHealthCheck(IOptions<SmtpOptions> options)
    {
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var smtp = _options.Value;
        if (!HasValidConfiguration(smtp))
        {
            return HealthCheckResult.Unhealthy("SMTP configuration is incomplete or invalid.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ConnectionTimeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, timeoutSource.Token);
            return HealthCheckResult.Healthy("SMTP host is reachable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SMTP host is unavailable.", exception);
        }
    }

    private static bool HasValidConfiguration(SmtpOptions smtp)
    {
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            return false;
        }

        if (smtp.Port < 1 || smtp.Port > 65535)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(smtp.UserName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(smtp.Password))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(smtp.FromAddress))
        {
            return false;
        }

        var usesStartTls = string.Equals(
            smtp.Security,
            "StartTls",
            StringComparison.OrdinalIgnoreCase);
        if (usesStartTls)
        {
            return true;
        }

        var usesSslOnConnect = string.Equals(
            smtp.Security,
            "SslOnConnect",
            StringComparison.OrdinalIgnoreCase);
        return usesSslOnConnect;
    }
}
