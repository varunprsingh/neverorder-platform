using NeverOrder.Domain.Common;

namespace NeverOrder.Domain.Orders;

public sealed class EmptyOrderException : DomainException
{
    public EmptyOrderException() : base("An order must contain at least one item.")
    {
    }
}

public sealed class Order
{
    private readonly List<OrderItem> _items = new();
    private readonly List<OrderStatusHistory> _statusHistory = new();

    private Order()
    {
    }

    private Order(Guid userId, string orderNumber, IReadOnlyCollection<OrderItem> items, decimal deliveryFee, DateTimeOffset now)
    {
        if (items.Count == 0)
        {
            throw new EmptyOrderException();
        }

        Id = Guid.NewGuid();
        OrderNumber = orderNumber;
        UserId = userId;
        Status = OrderStatus.Created;
        CreatedAt = now;
        UpdatedAt = now;

        _items.AddRange(items);

        Subtotal = decimal.Round(_items.Sum(i => i.LineTotal), 2, MidpointRounding.AwayFromZero);
        DeliveryFee = decimal.Round(deliveryFee, 2, MidpointRounding.AwayFromZero);
        Total = Subtotal + DeliveryFee;

        _statusHistory.Add(new OrderStatusHistory(null, OrderStatus.Created, now, "Virtual order placed."));
    }

    public Guid Id { get; private set; }

    public string OrderNumber { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal DeliveryFee { get; private set; }

    public decimal Total { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// When the simulation should advance this order next. Persisting it rather than holding an
    /// in-memory timer is what lets progression resume after a worker restart.
    /// </summary>
    public DateTimeOffset? NextTransitionAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderStatusHistory> StatusHistory => _statusHistory;

    public static Order Create(
        Guid userId,
        string orderNumber,
        IReadOnlyCollection<OrderItem> items,
        decimal deliveryFee,
        DateTimeOffset now) => new(userId, orderNumber, items, deliveryFee, now);

    public void TransitionTo(OrderStatus next, DateTimeOffset now, string? note = null)
    {
        if (!OrderStateMachine.CanTransition(Status, next))
        {
            throw new InvalidOrderTransitionException(Id, Status, next);
        }

        _statusHistory.Add(new OrderStatusHistory(Status, next, now, note));
        Status = next;
        UpdatedAt = now;

        if (OrderStateMachine.IsTerminal(next))
        {
            NextTransitionAt = null;
        }
    }

    public void ScheduleNextTransition(DateTimeOffset? at)
    {
        NextTransitionAt = OrderStateMachine.IsTerminal(Status) ? null : at;
    }
}
