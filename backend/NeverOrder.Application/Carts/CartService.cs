using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Common;
using NeverOrder.Application.Orders;
using NeverOrder.Domain.Carts;
using NeverOrder.Domain.Catalog;

namespace NeverOrder.Application.Carts;

public sealed class CartService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly CheckoutOptions _checkout;

    public CartService(IApplicationDbContext db, IClock clock, IOptions<CheckoutOptions> checkout)
    {
        _db = db;
        _clock = clock;
        _checkout = checkout.Value;
    }

    public async Task<CartDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        return await ToDtoAsync(cart, ct);
    }

    public async Task<CartDto> AddItemAsync(Guid userId, Guid productId, int quantity, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        var product = await RequireOrderableProductAsync(productId, ct);

        var alreadyInCart = cart.Items.SingleOrDefault(i => i.ProductId == productId)?.Quantity ?? 0;
        RequireStock(product, alreadyInCart + quantity);

        cart.AddOrIncrease(product.Id, product.Name, product.Price, quantity, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(cart, ct);
    }

    public async Task<CartDto> UpdateItemAsync(Guid userId, Guid productId, int quantity, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        var product = await RequireOrderableProductAsync(productId, ct);
        RequireStock(product, quantity);

        cart.SetQuantity(productId, quantity, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(cart, ct);
    }

    public async Task<CartDto> RemoveItemAsync(Guid userId, Guid productId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        cart.Remove(productId, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(cart, ct);
    }

    public async Task<CartDto> ClearAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        cart.Clear(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(cart, ct);
    }

    private async Task<Cart> LoadCartAsync(Guid userId, CancellationToken ct)
    {
        var cart = await _db.Carts
            .Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is not null)
        {
            return cart;
        }

        cart = new Cart(userId, _clock.UtcNow);
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync(ct);

        return cart;
    }

    private async Task<Product> RequireOrderableProductAsync(Guid productId, CancellationToken ct)
    {
        var product = await _db.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null || !product.IsActive)
        {
            throw new NotFoundException("Product");
        }

        return product;
    }

    private static void RequireStock(Product product, int requestedQuantity)
    {
        if (requestedQuantity > product.AvailableQuantity)
        {
            throw new InvalidCartOperationException(
                $"Only {product.AvailableQuantity} of '{product.Name}' are available.");
        }
    }

    private async Task<CartDto> ToDtoAsync(Cart cart, CancellationToken ct)
    {
        var productIds = cart.Items.Select(i => i.ProductId).ToList();

        var catalog = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ImageUrl, p.IsActive, p.AvailableQuantity })
            .ToDictionaryAsync(p => p.Id, ct);

        var items = cart.Items
            .OrderBy(i => i.AddedAt)
            .Select(i =>
            {
                var found = catalog.TryGetValue(i.ProductId, out var product);
                return new CartItemDto(
                    i.ProductId,
                    i.ProductName,
                    found ? product!.ImageUrl : string.Empty,
                    i.UnitPrice,
                    i.Quantity,
                    i.LineTotal,
                    found && product!.IsActive && product.AvailableQuantity >= i.Quantity);
            })
            .ToList();

        var subtotal = cart.Subtotal;
        var deliveryFee = items.Count == 0 ? 0m : _checkout.DeliveryFee;

        return new CartDto(
            cart.Id,
            items,
            subtotal,
            deliveryFee,
            subtotal + deliveryFee,
            items.Sum(i => i.Quantity));
    }
}
