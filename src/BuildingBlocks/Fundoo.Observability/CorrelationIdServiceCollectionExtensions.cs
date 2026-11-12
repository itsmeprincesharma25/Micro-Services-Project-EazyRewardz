using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.Observability;

public static class CorrelationIdServiceCollectionExtensions
{
    public static IServiceCollection AddFundooCorrelationIds(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<CorrelationIdDelegatingHandler>();
        return services;
    }
}
