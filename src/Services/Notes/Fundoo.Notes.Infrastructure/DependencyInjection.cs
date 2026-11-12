using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Features.Labels;
using Fundoo.Notes.Application.Features.Notes;
using Fundoo.Notes.Infrastructure.Caching;
using Fundoo.Notes.Infrastructure.Persistence;
using Fundoo.Notes.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Fundoo.Observability;

namespace Fundoo.Notes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotesDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'NotesDatabase' is not configured.");
        }

        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        var redisConnectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException("Redis connection string is not configured.");
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "FundooNotes:";
        });
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<INoteCache, RedisNoteCache>();
        var userServiceBaseUrl = configuration["Services:User:BaseUrl"];
        var userServiceKey = configuration["InternalApi:UserServiceKey"];
        if (!Uri.TryCreate(userServiceBaseUrl, UriKind.Absolute, out var userServiceUri))
        {
            throw new InvalidOperationException("User service base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(userServiceKey) || userServiceKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Internal API key for the User service must be configured and contain at least 32 characters.");
        }

        services.AddHttpClient<IUserDirectoryClient, HttpUserDirectoryClient>(client =>
        {
            client.BaseAddress = userServiceUri;
            // The resilience handler below owns the request timeout.
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Add("X-Fundoo-Service-Key", userServiceKey);
        })
        .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);

            // This POST is a read-only lookup, so retrying it does not create duplicate data.
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;

            // Allow at most 10 simultaneous collaborator lookups to the User service.
            options.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 10;
            options.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 0;

            // Open after at least 5 failed calls in a 30-second sample; probe again after 10 seconds.
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.FailureRatio = 1.0;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<NoteService>();
        services.AddScoped<LabelService>();
        return services;
    }
}
