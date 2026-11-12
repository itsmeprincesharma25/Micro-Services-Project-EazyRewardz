using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Fundoo.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    internal const string ItemKey = "Fundoo.CorrelationId";
    private const int MaximumLength = 128;
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        if (!IsValid(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                continue;
            }

            if (character == '-')
            {
                continue;
            }

            if (character == '_')
            {
                continue;
            }

            if (character == '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
