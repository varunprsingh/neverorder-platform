using NeverOrder.Domain.Common;

namespace NeverOrder.Domain.Orders;

public sealed class InvalidOrderTransitionException : DomainException
{
    public InvalidOrderTransitionException(Guid orderId, OrderStatus from, OrderStatus to)
        : base($"Order {orderId} cannot move from {from} to {to}.")
    {
        OrderId = orderId;
        From = from;
        To = to;
    }

    public Guid OrderId { get; }

    public OrderStatus From { get; }

    public OrderStatus To { get; }
}
