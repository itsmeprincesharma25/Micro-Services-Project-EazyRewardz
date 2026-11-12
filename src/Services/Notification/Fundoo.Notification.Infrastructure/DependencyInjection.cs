using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.Features;
using Fundoo.Notification.Infrastructure.Email;
using Fundoo.Notification.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fundoo.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        var connectionString = configuration.GetConnectionString("NotificationDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'NotificationDatabase' is not configured.");
        }

        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddScoped<IEmailRequestStore, PostgresEmailRequestStore>();
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        services.AddScoped<NotificationService>();
        return services;
    }
}
