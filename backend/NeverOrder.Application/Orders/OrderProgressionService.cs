using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Common;
using NeverOrder.Domain.Events;
using NeverOrder.Domain.Orders;

namespace NeverOrder.Application.Orders;

/// <summary>
/// Owns every order state change. Confirmation is driven by the broker (OrderCreated consumer);
/// everything after it is driven by the scheduler reading Order.NextTransitionAt. Splitting the
/// work this way means exactly one writer is responsible for any given transition.
/// </summary>
public sealed class OrderProgressionService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IEventPublisher _publisher;
    private readonly IOrderNotifier _notifier;
    private readonly OrderSimulationOptions _options;
    private readonly ILogger<OrderProgressionService> _logger;

    public OrderProgressionService(
        IApplicationDbContext db,
        IClock clock,
        IEventPublisher publisher,
        IOrderNotifier notifier,
        IOptions<OrderSimulationOptions> options,
        ILogger<OrderProgressionService> logger)
    {
        _db = db;
        _clock = clock;
        _publisher = publisher;
        _notifier = notifier;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Created -> Confirmed, in response to the OrderCreated event.</summary>
    public async Task<bool> ConfirmAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException("Order");

        if (order.Status != OrderStatus.Created)
        {
            // A redelivered or duplicated event: the work is already done.
            _logger.LogDebug(
                "Order {OrderId} is already {Status}; skipping confirmation", orderId, order.Status);
            return false;
        }

        return await AdvanceAsync(order, ct);
    }

    /// <summary>
    /// Advances every order whose scheduled transition time has passed. <paramref name="includeCreated"/>
    /// is set only when the broker is disabled, so the scheduler covers confirmation too; otherwise
    /// Created -> Confirmed stays the exclusive job of the OrderCreated consumer.
    /// </summary>
    public async Task<int> AdvanceDueOrdersAsync(bool includeCreated = false, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var due = await _db.Orders
            .Where(o => o.NextTransitionAt != null
                        && o.NextTransitionAt <= now
                        && (includeCreated || o.Status != OrderStatus.Created)
                        && o.Status != OrderStatus.Delivered
                        && o.Status != OrderStatus.Failed)
            .OrderBy(o => o.NextTransitionAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        var advanced = 0;
        foreach (var order in due)
        {
            if (await AdvanceAsync(order, ct))
            {
                advanced++;
            }
        }

        return advanced;
    }

    private async Task<bool> AdvanceAsync(Order order, CancellationToken ct)
    {
        var next = OrderStateMachine.NextOnHappyPath(order.Status);
        if (next is null)
        {
            return false;
        }

        var from = order.Status;
        var now = _clock.UtcNow;

        order.TransitionTo(next.Value, now, NoteFor(next.Value));

        var delay = _options.DelayAfter(next.Value);
        order.ScheduleNextTransition(delay is null ? null : now + delay.Value);

        await _publisher.PublishAsync(
            new OrderStatusChangedEvent(order.Id, order.UserId, order.OrderNumber, from, next.Value),
            ct);

        try
        {
            // The transition and its outbox row commit together.
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another instance advanced this order between our read and our write. Its transition
            // stands; ours never happened, so the outbox row must go with it.
            DiscardPendingChanges();

            _logger.LogDebug(
                "Order {OrderId} was advanced concurrently; skipping {FromStatus} -> {ToStatus}",
                order.Id, from, next.Value);

            return false;
        }

        _logger.LogInformation(
            "Order {OrderNumber} ({OrderId}) moved {FromStatus} -> {ToStatus}",
            order.OrderNumber, order.Id, from, next.Value);

        await NotifyAsync(order, from, next.Value, now, ct);

        return true;
    }

    /// <summary>
    /// Raised here rather than from the broker consumer because confirmation is the consumer's job only
    /// while the broker is enabled; hanging live updates off the transition itself keeps them working
    /// in both configurations. Told after the commit, and never allowed to undo it.
    /// </summary>
    private async Task NotifyAsync(
        Order order,
        OrderStatus from,
        OrderStatus to,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            await _notifier.OrderStatusChangedAsync(
                order.UserId,
                new OrderStatusNotification(
                    order.Id,
                    order.OrderNumber,
                    from.ToString(),
                    to.ToString(),
                    now,
                    OrderStateMachine.IsTerminal(to)),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "Could not announce {OrderId} reaching {Status}; the transition still stands",
                order.Id, to);
        }
    }

    /// <summary>
    /// Each order is saved on its own, so anything still pending belongs to the one that just lost the
    /// race. Detaching it keeps the failed write out of the next order's SaveChanges.
    /// </summary>
    private void DiscardPendingChanges()
    {
        var pending = _db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in pending)
        {
            entry.State = EntityState.Detached;
        }
    }

    private static string NoteFor(OrderStatus status) => status switch
    {
        OrderStatus.Confirmed => "Virtual order confirmed.",
        OrderStatus.Preparing => "Your virtual items are being prepared.",
        OrderStatus.OutForDelivery => "Out for virtual delivery.",
        OrderStatus.Delivered => "Virtually delivered. No payment was charged.",
        _ => string.Empty
    };
}
