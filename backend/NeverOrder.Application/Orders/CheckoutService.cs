using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Common;
using NeverOrder.Domain.Events;
using NeverOrder.Domain.Orders;

namespace NeverOrder.Application.Orders;

public sealed class CheckoutService
{
    private const string VirtualNotice = "No payment was charged. This is a simulated order.";

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IEventPublisher _publisher;
    private readonly IOrderNumberGenerator _orderNumbers;
    private readonly CheckoutOptions _checkout;
    private readonly OrderSimulationOptions _simulation;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IApplicationDbContext db,
        IClock clock,
        IEventPublisher publisher,
        IOrderNumberGenerator orderNumbers,
        IOptions<CheckoutOptions> checkout,
        IOptions<OrderSimulationOptions> simulation,
        ILogger<CheckoutService> logger)
    {
        _db = db;
        _clock = clock;
        _publisher = publisher;
        _orderNumbers = orderNumbers;
        _checkout = checkout.Value;
        _simulation = simulation.Value;
        _logger = logger;
    }

    public async Task<OrderPlacedDto> CheckoutAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await _db.Carts
            .Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is null || cart.IsEmpty)
        {
            throw new CheckoutValidationException("Your cart is empty.");
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // Prices come from the live catalog, never from the cart snapshot or the client.
        var orderItems = new List<OrderItem>(cart.Items.Count);
        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
            {
                throw new CheckoutValidationException($"'{item.ProductName}' is no longer available.");
            }

            if (item.Quantity > product.AvailableQuantity)
            {
                throw new CheckoutValidationException(
                    $"Only {product.AvailableQuantity} of '{product.Name}' are available.");
            }

            orderItems.Add(new OrderItem(product.Id, product.Name, product.Price, item.Quantity));
        }

        var now = _clock.UtcNow;
        var order = Order.Create(userId, _orderNumbers.Next(now), orderItems, _checkout.DeliveryFee, now);

        var firstDelay = _simulation.DelayAfter(OrderStatus.Created);
        order.ScheduleNextTransition(firstDelay is null ? null : now + firstDelay.Value);

        _db.Orders.Add(order);
        cart.Clear(now);

        await _publisher.PublishAsync(
            new OrderCreatedEvent(order.Id, userId, order.OrderNumber, order.Total, order.Items.Count),
            ct);

        // One SaveChanges => one transaction covering the order, its items, its history, the cart
        // reset and the outbox row. The event cannot be lost while the order survives.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Virtual order {OrderNumber} ({OrderId}) created for user {UserId} with {ItemCount} items totalling {Total}",
            order.OrderNumber, order.Id, userId, order.Items.Count, order.Total);

        return new OrderPlacedDto(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.Subtotal,
            order.DeliveryFee,
            order.Total,
            order.CreatedAt,
            VirtualNotice);
    }
}
