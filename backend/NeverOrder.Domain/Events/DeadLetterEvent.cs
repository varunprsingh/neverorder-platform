namespace NeverOrder.Domain.Events;

/// <summary>A message that exhausted its retries, kept so an operator can inspect and replay it.</summary>
public sealed class DeadLetterEvent
{
    private DeadLetterEvent()
    {
    }

    public DeadLetterEvent(
        Guid eventId,
        string eventType,
        string payload,
        string failureReason,
        int attempts,
        DateTimeOffset failedAt)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        EventType = eventType;
        Payload = payload;
        FailureReason = failureReason;
        Attempts = attempts;
        FirstFailedAt = failedAt;
        LastFailedAt = failedAt;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public string FailureReason { get; private set; } = null!;

    public int Attempts { get; private set; }

    public DateTimeOffset FirstFailedAt { get; private set; }

    public DateTimeOffset LastFailedAt { get; private set; }

    public DateTimeOffset? ReplayedAt { get; private set; }

    public void MarkReplayed(DateTimeOffset now) => ReplayedAt = now;
}
