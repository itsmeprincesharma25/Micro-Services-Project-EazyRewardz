using Fundoo.User.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fundoo.User.Api.Health;

public sealed class UserPostgresHealthCheck : IHealthCheck
{
    private readonly UserDbContext _dbContext;

    public UserPostgresHealthCheck(UserDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("User PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("User PostgreSQL is unavailable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "User PostgreSQL is unavailable.",
                exception);
        }
    }
}
