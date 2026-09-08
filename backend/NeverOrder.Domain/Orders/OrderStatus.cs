namespace NeverOrder.Domain.Orders;

public enum OrderStatus
{
    Created = 0,
    Confirmed = 1,
    Preparing = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Failed = 5
}
