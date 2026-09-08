using NeverOrder.Infrastructure.Services;
using Serilog;
using Serilog.Context;

namespace NeverOrder.Api.Middleware;

public sealed class CorrelationIdMiddleware : IMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly IDiagnosticContext _diagnosticContext;

    public CorrelationIdMiddleware(IDiagnosticContext diagnosticContext) =>
        _diagnosticContext = diagnosticContext;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = Resolve(context);

        context.RequestServices.GetRequiredService<CorrelationContext>().CorrelationId = correlationId;

        // Serilog's request-logging middleware writes its own completion event, which sees the
        // diagnostic context rather than the ambient LogContext.
        _diagnosticContext.Set("CorrelationId", correlationId);
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    /// <summary>
    /// An inbound id is honoured so a caller can stitch its logs to ours, but it is length-capped and
    /// echoed only after sanitising: it lands in a response header and in every log line.
    /// </summary>
    private static string Resolve(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(inbound) || inbound.Length > 128 || !IsSafe(inbound))
        {
            return Guid.NewGuid().ToString("n");
        }

        return inbound;
    }

    private static bool IsSafe(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.' or ':'))
            {
                return false;
            }
        }

        return true;
    }
}
