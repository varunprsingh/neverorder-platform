using NeverOrder.Application.Abstractions;

namespace NeverOrder.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Mutable so the API can seed it from the request header. Work that no request caused — the outbox
/// drain, the progression worker — resolves it in its own scope and simply leaves it empty.
/// </summary>
public sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Human-friendly, sortable and collision-resistant enough for a single-node demo:
/// NO-yyyyMMdd-HHmmss-XXXX where the suffix is random.
/// </summary>
public sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string Next(DateTimeOffset now)
    {
        Span<char> suffix = stackalloc char[4];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }

        return $"NO-{now.UtcDateTime:yyyyMMdd-HHmmss}-{new string(suffix)}";
    }
}
