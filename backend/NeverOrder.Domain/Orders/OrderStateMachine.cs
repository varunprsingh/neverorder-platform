namespace NeverOrder.Domain.Orders;

/// <summary>
/// The single source of truth for legal order transitions. The API, the workers and the
/// tests all go through here so no caller can invent its own rules.
/// </summary>
public static class OrderStateMachine
{
    private static readonly IReadOnlyDictionary<OrderStatus, IReadOnlySet<OrderStatus>> AllowedTransitions =
        new Dictionary<OrderStatus, IReadOnlySet<OrderStatus>>
        {
            [OrderStatus.Created] = new HashSet<OrderStatus> { OrderStatus.Confirmed, OrderStatus.Failed },
            [OrderStatus.Confirmed] = new HashSet<OrderStatus> { OrderStatus.Preparing, OrderStatus.Failed },
            [OrderStatus.Preparing] = new HashSet<OrderStatus> { OrderStatus.OutForDelivery, OrderStatus.Failed },
            [OrderStatus.OutForDelivery] = new HashSet<OrderStatus> { OrderStatus.Delivered, OrderStatus.Failed },
            [OrderStatus.Delivered] = new HashSet<OrderStatus>(),
            [OrderStatus.Failed] = new HashSet<OrderStatus>()
        };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static IReadOnlySet<OrderStatus> AllowedFrom(OrderStatus from) =>
        AllowedTransitions.TryGetValue(from, out var allowed) ? allowed : new HashSet<OrderStatus>();

    /// <summary>The next state the simulation advances to, or null once the order is terminal.</summary>
    public static OrderStatus? NextOnHappyPath(OrderStatus from) => from switch
    {
        OrderStatus.Created => OrderStatus.Confirmed,
        OrderStatus.Confirmed => OrderStatus.Preparing,
        OrderStatus.Preparing => OrderStatus.OutForDelivery,
        OrderStatus.OutForDelivery => OrderStatus.Delivered,
        _ => null
    };

    public static bool IsTerminal(OrderStatus status) =>
        status is OrderStatus.Delivered or OrderStatus.Failed;
}
