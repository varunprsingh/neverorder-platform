namespace NeverOrder.Application.Orders;

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderStatusHistoryDto(
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    string? Note);

public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    string Status,
    decimal Total,
    int ItemCount);

public sealed record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Status,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory);

public sealed record TrackingStepDto(
    string Status,
    string Label,
    bool IsReached,
    bool IsCurrent,
    DateTimeOffset? OccurredAt);

public sealed record OrderTrackingDto(
    Guid Id,
    string OrderNumber,
    string Status,
    bool IsComplete,
    DateTimeOffset? NextTransitionAt,
    IReadOnlyList<TrackingStepDto> Steps);

public sealed record OrderPlacedDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    DateTimeOffset CreatedAt,
    string Notice);
