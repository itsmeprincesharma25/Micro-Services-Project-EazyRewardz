using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.Features.Authentication;
using Fundoo.User.Infrastructure.Persistence;
using Fundoo.User.Infrastructure.Notifications;
using Fundoo.User.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Fundoo.Observability;

namespace Fundoo.User.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUserInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("UserDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'UserDatabase' is not configured.");
        }

        services.AddDbContext<UserDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddHttpClient<IPasswordResetEmailSender, HttpPasswordResetEmailSender>(
            (serviceProvider, client) =>
            {
                var baseAddress = configuration["Notification:BaseUrl"];
                if (string.IsNullOrWhiteSpace(baseAddress))
                {
                    throw new InvalidOperationException(
                        "Notification:BaseUrl is not configured.");
                }

                client.BaseAddress = new Uri(baseAddress);
                // The resilience handler below owns request timeouts.
                client.Timeout = Timeout.InfiniteTimeSpan;

                var serviceKey = configuration["Notification:ApiKey"];
                if (!string.IsNullOrWhiteSpace(serviceKey))
                {
                    client.DefaultRequestHeaders.Add("X-Fundoo-Service-Key", serviceKey);
                }
            })
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);

                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(500);
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // Limit simultaneous outgoing email requests to protect Notification.
                options.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 20;
                options.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 0;
            });
        services.AddScoped<UserAccountService>();
        return services;
    }
}
