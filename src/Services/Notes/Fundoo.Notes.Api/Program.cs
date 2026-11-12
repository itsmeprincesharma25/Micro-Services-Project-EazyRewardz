using System.Text;
using Fundoo.Notes.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using FluentValidation;
using FluentValidation.AspNetCore;
using Fundoo.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "Fundoo.Notes.Api"));

builder.Services.AddControllers();
builder.Services.AddFundooCorrelationIds();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Fundoo.Notes.Api.Validation.CreateNoteRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddNotesInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck("liveness", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<Fundoo.Notes.Api.Health.NotesPostgresHealthCheck>(
        "postgres",
        tags: new[] { "ready" })
    .AddCheck<Fundoo.Notes.Api.Health.NotesRedisHealthCheck>(
        "redis",
        tags: new[] { "ready" });
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var signingKey = builder.Configuration["Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException("JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("JWT audience is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(signingKey ?? string.Empty) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be configured and contain at least 32 bytes.");
        }

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();
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
