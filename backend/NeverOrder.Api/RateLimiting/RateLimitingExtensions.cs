using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace NeverOrder.Api.RateLimiting;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitWindow Global { get; set; } = new() { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) };

    /// <summary>Register and login only. Tight enough to make credential stuffing impractical.</summary>
    public RateLimitWindow Auth { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };
}

public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }
}

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddNeverOrderRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    CallerKey(context),
                    _ => Window(options.Global)));

            // Credentials are not yet known here, so this one can only be keyed by address.
            limiter.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"auth:{ClientAddress(context)}",
                    _ => Window(options.Auth)));

            limiter.OnRejected = WriteProblemDetailsAsync;
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions Window(RateLimitWindow window) => new()
    {
        PermitLimit = window.PermitLimit,
        Window = window.Window,
        // Queueing would turn a burst into latency; rejecting immediately is honest and cheaper.
        QueueLimit = 0
    };

    /// <summary>
    /// Signed-in callers are limited per account so that everyone behind one NAT or proxy is not
    /// throttled as a single client. Anonymous traffic has nothing better than the address.
    /// </summary>
    private static string CallerKey(HttpContext context)
    {
        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return string.IsNullOrEmpty(userId)
            ? $"ip:{ClientAddress(context)}"
            : $"user:{userId}";
    }

    private static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static ValueTask WriteProblemDetailsAsync(OnRejectedContext context, CancellationToken ct)
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "You have made too many requests. Wait a moment and try again."
        };

        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
            problemDetails,
            problemDetails.GetType(),
            options: null,
            contentType: "application/problem+json",
            ct));
    }
}
