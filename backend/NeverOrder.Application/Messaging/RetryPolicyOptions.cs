namespace NeverOrder.Application.Messaging;

public sealed class RetryPolicyOptions
{
    public const string SectionName = "Retry";

    /// <summary>Total delivery attempts before a message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>
    /// Backoff ladder. Each step must have a matching TTL queue declared in the broker topology,
    /// so the values are treated as a fixed, declared set rather than computed on the fly.
    /// </summary>
    public int[] BackoffSeconds { get; set; } = { 1, 2, 4, 8 };

    /// <summary>
    /// The wait before <paramref name="attempt"/> (1-based) is retried, or null once retries are
    /// exhausted. The ladder's last value repeats if MaxAttempts exceeds its length.
    /// </summary>
    public TimeSpan? DelayForAttempt(int attempt)
    {
        if (attempt < 1 || attempt >= MaxAttempts || BackoffSeconds.Length == 0)
        {
            return null;
        }

        var index = Math.Min(attempt - 1, BackoffSeconds.Length - 1);
        return TimeSpan.FromSeconds(BackoffSeconds[index]);
    }

    public IReadOnlyList<int> DeclaredDelays => BackoffSeconds.Distinct().OrderBy(s => s).ToList();
}

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public int BatchSize { get; set; } = 50;

    /// <summary>Backoff applied when the broker rejects or is unreachable.</summary>
    public TimeSpan PublishRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
