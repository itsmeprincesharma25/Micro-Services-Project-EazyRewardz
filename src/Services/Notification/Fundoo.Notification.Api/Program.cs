using Fundoo.Notification.Api.Services;
using Fundoo.Notification.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Fundoo.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "Fundoo.Notification.Api"));

builder.Services.AddControllers();
builder.Services.AddFundooCorrelationIds();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Fundoo.Notification.Api.Validation.PasswordResetEmailRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddNotificationInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IInternalServiceKeyValidator, InternalServiceKeyValidator>();
builder.Services.AddHealthChecks()
    .AddCheck("liveness", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<Fundoo.Notification.Api.Health.NotificationPostgresHealthCheck>(
        "postgres",
        tags: new[] { "ready" })
    .AddCheck<Fundoo.Notification.Api.Health.NotificationSmtpHealthCheck>(
        "smtp",
        tags: new[] { "ready" });

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<TraceIdLogContextMiddleware>();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
