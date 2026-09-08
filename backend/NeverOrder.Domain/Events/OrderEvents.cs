using NeverOrder.Domain.Orders;

namespace NeverOrder.Domain.Events;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid UserId,
    string OrderNumber,
    decimal Total,
    int ItemCount) : IDomainEvent
{
    public string EventType => EventTypes.OrderCreated;

    public Guid AggregateId => OrderId;
}

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    Guid UserId,
    string OrderNumber,
    OrderStatus FromStatus,
    OrderStatus ToStatus) : IDomainEvent
{
    public string EventType => EventTypes.OrderStatusChanged;

    public Guid AggregateId => OrderId;
}
