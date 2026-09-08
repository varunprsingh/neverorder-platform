namespace NeverOrder.Domain.Events;

/// <summary>
/// An event queued for publication in the same transaction as the state change that produced it.
/// This is what closes the "order committed but event lost" window.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid eventId,
        string eventType,
        Guid aggregateId,
        string payload,
        DateTimeOffset occurredAt)
    {
        EventId = eventId;
        EventType = eventType;
        AggregateId = aggregateId;
        Payload = payload;
        OccurredAt = occurredAt;
        NextAttemptAt = occurredAt;
    }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = null!;

    public Guid AggregateId { get; private set; }

    /// <summary>The fully serialised envelope, ready to go on the wire unchanged.</summary>
    public string Payload { get; private set; } = null!;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public bool IsPublished => ProcessedAt is not null;

    public void MarkPublished(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset retryAt)
    {
        Attempts++;
        LastError = error.Length > 1000 ? error[..1000] : error;
        NextAttemptAt = retryAt;
    }
}
