using Microsoft.EntityFrameworkCore;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Common;
using NeverOrder.Domain.Orders;

namespace NeverOrder.Application.Orders;

public sealed class OrderQueryService
{
    private static readonly OrderStatus[] HappyPath =
    {
        OrderStatus.Created,
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.OutForDelivery,
        OrderStatus.Delivered
    };

    private readonly IApplicationDbContext _db;

    public OrderQueryService(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<OrderSummaryDto>> GetHistoryAsync(
        Guid userId,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var orders = _db.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId);

        var totalCount = await orders.CountAsync(ct);

        var items = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.CreatedAt,
                o.Status.ToString(),
                o.Total,
                o.Items.Sum(i => i.Quantity)))
            .ToListAsync(ct);

        return new PagedResult<OrderSummaryDto>(items, normalizedPage, normalizedSize, totalCount);
    }

    public async Task<OrderDetailDto> GetByIdAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        // Scoped by userId in the predicate, so another user's id simply looks non-existent.
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId && o.UserId == userId)
            .Select(o => new OrderDetailDto(
                o.Id,
                o.OrderNumber,
                o.CreatedAt,
                o.UpdatedAt,
                o.Status.ToString(),
                o.Subtotal,
                o.DeliveryFee,
                o.Total,
                o.Items
                    .OrderBy(i => i.ProductName)
                    .Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal))
                    .ToList(),
                o.StatusHistory
                    .OrderBy(h => h.OccurredAt)
                    .Select(h => new OrderStatusHistoryDto(
                        h.FromStatus == null ? null : h.FromStatus.ToString(),
                        h.ToStatus.ToString(),
                        h.OccurredAt,
                        h.Note))
                    .ToList()))
            .SingleOrDefaultAsync(ct);

        return order ?? throw new NotFoundException("Order");
    }

    public async Task<OrderTrackingDto> GetTrackingAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId && o.UserId == userId)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.NextTransitionAt,
                History = o.StatusHistory
                    .Select(h => new { h.ToStatus, h.OccurredAt })
                    .ToList()
            })
            .SingleOrDefaultAsync(ct);

        if (order is null)
        {
            throw new NotFoundException("Order");
        }

        var reachedAt = order.History
            .GroupBy(h => h.ToStatus)
            .ToDictionary(g => g.Key, g => g.Min(h => h.OccurredAt));

        var steps = HappyPath
            .Select(status => new TrackingStepDto(
                status.ToString(),
                LabelFor(status),
                reachedAt.ContainsKey(status),
                order.Status == status,
                reachedAt.TryGetValue(status, out var at) ? at : null))
            .ToList();

        return new OrderTrackingDto(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            OrderStateMachine.IsTerminal(order.Status),
            order.NextTransitionAt,
            steps);
    }

    private static string LabelFor(OrderStatus status) => status switch
    {
        OrderStatus.Created => "Order Created",
        OrderStatus.Confirmed => "Confirmed",
        OrderStatus.Preparing => "Preparing",
        OrderStatus.OutForDelivery => "Out For Delivery",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.Failed => "Failed",
        _ => status.ToString()
    };
}
