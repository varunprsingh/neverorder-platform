namespace NeverOrder.Application.Carts;

public sealed record CartItemDto(
    Guid ProductId,
    string ProductName,
    string ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool IsAvailable);

public sealed record CartDto(
    Guid Id,
    IReadOnlyList<CartItemDto> Items,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    int ItemCount);
