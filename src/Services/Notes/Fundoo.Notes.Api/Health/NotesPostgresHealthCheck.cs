using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Fundoo.Notes.Api.Health;

public sealed class NotesPostgresHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;

    public NotesPostgresHealthCheck(NpgsqlDataSource dataSource)
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
            return HealthCheckResult.Healthy("Notes PostgreSQL is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Notes PostgreSQL is unavailable.",
                exception);
        }
    }
}
