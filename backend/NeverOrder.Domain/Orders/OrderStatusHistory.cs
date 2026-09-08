namespace NeverOrder.Domain.Orders;

public sealed class OrderStatusHistory
{
    private OrderStatusHistory()
    {
    }

    public OrderStatusHistory(OrderStatus? fromStatus, OrderStatus toStatus, DateTimeOffset occurredAt, string? note)
    {
        Id = Guid.NewGuid();
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
        Note = note;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>Null for the initial entry recorded when the order is created.</summary>
    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string? Note { get; private set; }
}
