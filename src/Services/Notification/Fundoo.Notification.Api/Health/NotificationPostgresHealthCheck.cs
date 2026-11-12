using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Fundoo.Notification.Api.Health;

public sealed class NotificationPostgresHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;

    public NotificationPostgresHealthCheck(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("Notification PostgreSQL is reachable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Notification PostgreSQL is unavailable.",
                exception);
        }
    }
}
