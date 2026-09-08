namespace NeverOrder.Domain.Events;

public interface IDomainEvent
{
    string EventType { get; }

    Guid AggregateId { get; }
}

public static class EventTypes
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderStatusChanged = "OrderStatusChanged";

    /// <summary>Topic-exchange routing keys. Consumers bind to <c>order.*</c>.</summary>
    public static string RoutingKeyFor(string eventType) => eventType switch
    {
        OrderCreated => "order.created",
        OrderStatusChanged => "order.status-changed",
        _ => "order.unknown"
    };
}
