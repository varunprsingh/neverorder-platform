using NeverOrder.Domain.Events;

namespace NeverOrder.Application.Abstractions;

/// <summary>Abstracted so business code never depends on the broker, and tests can capture events.</summary>
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>Injected rather than calling DateTimeOffset.UtcNow, so time-based rules stay testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IOrderNumberGenerator
{
    string Next(DateTimeOffset now);
}

public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}

/// <summary>
/// The id that ties an HTTP request to everything it causes, including work that happens later on a
/// background worker. Events carry it across the broker so a consumer's logs point back to the request.
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
}
