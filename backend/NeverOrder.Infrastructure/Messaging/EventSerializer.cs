using System.Text.Json;
using NeverOrder.Domain.Events;

namespace NeverOrder.Infrastructure.Messaging;

public static class EventSerializer
{
    /// <summary>
    /// Produces the exact bytes that go on the wire. The outbox stores this verbatim so the
    /// publisher never has to re-derive a payload it might get subtly wrong.
    /// </summary>
    public static (Guid EventId, string Json) Serialize(
        IDomainEvent domainEvent,
        DateTimeOffset occurredAt,
        string? correlationId = null)
    {
        var eventId = Guid.NewGuid();

        var envelope = new
        {
            eventId,
            eventType = domainEvent.EventType,
            occurredAt,
            aggregateId = domainEvent.AggregateId,
            correlationId,
            payload = domainEvent
        };

        return (eventId, JsonSerializer.Serialize(envelope, MessagingJson.Options));
    }
}
