namespace NeverOrder.Domain.Events;

/// <summary>
/// Written inside the same transaction as the work it guards. The composite primary key is the
/// idempotency mechanism: a duplicate delivery loses the insert race and is safely skipped.
/// </summary>
public sealed class ProcessedEvent
{
    private ProcessedEvent()
    {
    }

    public ProcessedEvent(Guid eventId, string consumerName, string eventType, DateTimeOffset processedAt)
    {
        EventId = eventId;
        ConsumerName = consumerName;
        EventType = eventType;
        ProcessedAt = processedAt;
    }

    public Guid EventId { get; private set; }

    /// <summary>Part of the key so independent consumers can each process the same event once.</summary>
    public string ConsumerName { get; private set; } = null!;

    public string EventType { get; private set; } = null!;

    public DateTimeOffset ProcessedAt { get; private set; }
}
