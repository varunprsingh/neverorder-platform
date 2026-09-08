using NeverOrder.Domain.Orders;

namespace NeverOrder.Application.Orders;

public sealed class CheckoutOptions
{
    public const string SectionName = "Checkout";

    /// <summary>Always zero in a virtual store, but configurable so the maths stays real.</summary>
    public decimal DeliveryFee { get; set; }
}

public sealed class OrderSimulationOptions
{
    public const string SectionName = "OrderSimulation";

    public TimeSpan CreatedToConfirmed { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan ConfirmedToPreparing { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan PreparingToOutForDelivery { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan OutForDeliveryToDelivered { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>How often the progression worker looks for orders that are due.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public int BatchSize { get; set; } = 50;

    /// <summary>How long to wait in <paramref name="current"/> before advancing; null if terminal.</summary>
    public TimeSpan? DelayAfter(OrderStatus current) => current switch
    {
        OrderStatus.Created => CreatedToConfirmed,
        OrderStatus.Confirmed => ConfirmedToPreparing,
        OrderStatus.Preparing => PreparingToOutForDelivery,
        OrderStatus.OutForDelivery => OutForDeliveryToDelivered,
        _ => null
    };
}
