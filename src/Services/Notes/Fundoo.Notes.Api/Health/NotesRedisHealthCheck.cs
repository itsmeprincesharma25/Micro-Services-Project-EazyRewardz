using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fundoo.Notes.Api.Health;

public sealed class NotesRedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;

    public NotesRedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var key = $"health:notes:{Guid.NewGuid():N}";
        var value = Guid.NewGuid().ToString("N");
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
        };

        try
        {
            await _cache.SetStringAsync(key, value, options, cancellationToken);
            var storedValue = await _cache.GetStringAsync(key, cancellationToken);
            await _cache.RemoveAsync(key, cancellationToken);

            return string.Equals(storedValue, value, StringComparison.Ordinal)
                ? HealthCheckResult.Healthy("Notes Redis is reachable.")
                : HealthCheckResult.Unhealthy("Notes Redis health-check value did not match.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Notes Redis is unavailable.", exception);
        }
    }
}
