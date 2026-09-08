using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Orders;
using NeverOrder.Domain.Events;
using Npgsql;

namespace NeverOrder.Infrastructure.Messaging;

public enum DispatchOutcome
{
    Handled,
    Duplicate,
    Unknown
}

/// <summary>
/// Runs a delivery exactly once. The ProcessedEvent row is added before the handler runs and
/// committed by the same SaveChanges, so the guard and the work it protects share one transaction.
/// A duplicate that slips past the pre-check loses the primary-key race instead.
/// </summary>
public sealed class EventDispatcher
{
    public const string ConsumerName = "orders";

    private readonly IApplicationDbContext _db;
    private readonly OrderProgressionService _progression;
    private readonly IClock _clock;
    private readonly ILogger<EventDispatcher> _logger;

    public EventDispatcher(
        IApplicationDbContext db,
        OrderProgressionService progression,
        IClock clock,
        ILogger<EventDispatcher> logger)
    {
        _db = db;
        _progression = progression;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DispatchOutcome> DispatchAsync(EventEnvelope envelope, CancellationToken ct = default)
    {
        var alreadyProcessed = await _db.ProcessedEvents
            .AnyAsync(e => e.EventId == envelope.EventId && e.ConsumerName == ConsumerName, ct);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Skipping duplicate {EventType} {EventId}", envelope.EventType, envelope.EventId);
            return DispatchOutcome.Duplicate;
        }

        _db.ProcessedEvents.Add(
            new ProcessedEvent(envelope.EventId, ConsumerName, envelope.EventType, _clock.UtcNow));

        var outcome = await HandleAsync(envelope, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogInformation(
                "Concurrent duplicate of {EventType} {EventId} was rejected by the database",
                envelope.EventType, envelope.EventId);
            return DispatchOutcome.Duplicate;
        }

        return outcome;
    }

    private async Task<DispatchOutcome> HandleAsync(EventEnvelope envelope, CancellationToken ct)
    {
        switch (envelope.EventType)
        {
            case EventTypes.OrderCreated:
            {
                var payload = envelope.Payload.Deserialize<OrderCreatedEvent>(MessagingJson.Options)
                    ?? throw new InvalidOperationException($"{EventTypes.OrderCreated} payload was empty.");

                await _progression.ConfirmAsync(payload.OrderId, ct);
                return DispatchOutcome.Handled;
            }

            case EventTypes.OrderStatusChanged:
            {
                var payload = envelope.Payload.Deserialize<OrderStatusChangedEvent>(MessagingJson.Options)
                    ?? throw new InvalidOperationException($"{EventTypes.OrderStatusChanged} payload was empty.");

                // Phase 4 forwards this to SignalR for live tracking.
                _logger.LogDebug(
                    "Observed {OrderNumber} {FromStatus} -> {ToStatus}",
                    payload.OrderNumber, payload.FromStatus, payload.ToStatus);

                return DispatchOutcome.Handled;
            }

            default:
                _logger.LogWarning("No handler for event type {EventType}", envelope.EventType);
                return DispatchOutcome.Unknown;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
