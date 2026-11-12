using System.Threading.RateLimiting;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Fundoo.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "Fundoo.ApiGateway"));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddFundooCorrelationIds();

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var requestPath = GetRateLimitPath(context.Request.Path);
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = $"{requestPath}:{clientIp}";
        var permitLimit = IsLoginRequest(context.Request.Path) ? 5 : 100;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services
    .AddOcelot(builder.Configuration)
    .AddDelegatingHandler<CorrelationIdDelegatingHandler>(global: true)
    .AddQualityOfService();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<TraceIdLogContextMiddleware>();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
await app.UseOcelot();

app.Run();

static string GetRateLimitPath(PathString path)
{
    if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase))
    {
        return "/api/auth/login";
    }

    if (path.Equals("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase))
    {
        return "/api/auth/forgot-password";
    }

    return "/api";
}

static bool IsLoginRequest(PathString path)
{
    return path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);
}
