using Microsoft.Extensions.Logging;
using NeverOrder.Application.Abstractions;
using NeverOrder.Domain.Events;
namespace NeverOrder.Infrastructure.Messaging;

/// <summary>
/// Implements the publish abstraction by writing to the outbox rather than talking to the broker.
/// Callers therefore enqueue events inside their own transaction, and a broker outage can never
/// leave a committed state change without its event.
/// </summary>
public sealed class OutboxEventPublisher : IEventPublisher
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<OutboxEventPublisher> _logger;

    public OutboxEventPublisher(
        IApplicationDbContext db,
        IClock clock,
        ICorrelationContext correlation,
        ILogger<OutboxEventPublisher> logger)
    {
        _db = db;
        _clock = clock;
        _correlation = correlation;
        _logger = logger;
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var (eventId, json) = EventSerializer.Serialize(domainEvent, now, _correlation.CorrelationId);

        _db.OutboxMessages.Add(new OutboxMessage(eventId, domainEvent.EventType, domainEvent.AggregateId, json, now));

        _logger.LogDebug(
            "Queued {EventType} {EventId} for aggregate {AggregateId} in the outbox",
            domainEvent.EventType, eventId, domainEvent.AggregateId);

        return Task.CompletedTask;
    }
}

/// <summary>Used when the broker is switched off, so the rest of the system still runs.</summary>
public sealed class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) => _logger = logger;

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[broker disabled] {EventType} for aggregate {AggregateId}",
            domainEvent.EventType, domainEvent.AggregateId);

        return Task.CompletedTask;
    }
}
