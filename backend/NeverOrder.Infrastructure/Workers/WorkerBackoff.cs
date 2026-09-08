using Microsoft.Extensions.Logging;

namespace NeverOrder.Infrastructure.Workers;

/// <summary>
/// Keeps a polling worker quiet during an outage. Without this a 500ms poll loop emits a full
/// stack trace twice a second for as long as a dependency is down.
/// </summary>
internal sealed class WorkerBackoff
{
    private readonly TimeSpan _initial;
    private readonly TimeSpan _max;
    private int _consecutiveFailures;

    public WorkerBackoff(TimeSpan initial, TimeSpan max)
    {
        _initial = initial;
        _max = max;
    }

    /// <summary>Records a failure, logs it proportionately, and returns how long to pause.</summary>
    public TimeSpan RecordFailure(ILogger logger, Exception exception, string operation)
    {
        _consecutiveFailures++;

        var delay = TimeSpan.FromTicks(Math.Min(
            _initial.Ticks * (long)Math.Pow(2, Math.Min(_consecutiveFailures - 1, 16)),
            _max.Ticks));

        // Full detail once; a single line thereafter until it recovers.
        if (_consecutiveFailures == 1)
        {
            logger.LogError(exception, "{Operation} failed; pausing for {Delay}", operation, delay);
        }
        else
        {
            logger.LogWarning(
                "{Operation} still failing after {Count} attempts ({Reason}); next attempt in {Delay}",
                operation, _consecutiveFailures, exception.GetBaseException().Message, delay);
        }

        return delay;
    }

    public void RecordSuccess(ILogger logger, string operation)
    {
        if (_consecutiveFailures == 0)
        {
            return;
        }

        logger.LogInformation("{Operation} recovered after {Count} failed attempts", operation, _consecutiveFailures);
        _consecutiveFailures = 0;
    }
}
