using NeverOrder.Domain.Common;

namespace NeverOrder.Domain.Carts;

public sealed class InvalidCartOperationException : DomainException
{
    public InvalidCartOperationException(string message) : base(message)
    {
    }
}

public sealed class Cart
{
    private readonly List<CartItem> _items = new();

    private Cart()
    {
    }

    public Cart(Guid userId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<CartItem> Items => _items;

    public decimal Subtotal => decimal.Round(_items.Sum(i => i.LineTotal), 2, MidpointRounding.AwayFromZero);

    public bool IsEmpty => _items.Count == 0;

    public void AddOrIncrease(Guid productId, string productName, decimal unitPrice, int quantity, DateTimeOffset now)
    {
        RequirePositive(quantity);

        var existing = _items.SingleOrDefault(i => i.ProductId == productId);
        if (existing is null)
        {
            _items.Add(new CartItem(productId, productName, unitPrice, quantity, now));
        }
        else
        {
            existing.Increase(quantity);
            existing.RefreshSnapshot(productName, unitPrice);
        }

        UpdatedAt = now;
    }

    public void SetQuantity(Guid productId, int quantity, DateTimeOffset now)
    {
        RequirePositive(quantity);

        var existing = _items.SingleOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidCartOperationException("That product is not in the cart.");

        existing.SetQuantity(quantity);
        UpdatedAt = now;
    }

    public void Remove(Guid productId, DateTimeOffset now)
    {
        var existing = _items.SingleOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidCartOperationException("That product is not in the cart.");

        _items.Remove(existing);
        UpdatedAt = now;
    }

    public void Clear(DateTimeOffset now)
    {
        _items.Clear();
        UpdatedAt = now;
    }

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidCartOperationException("Quantity must be greater than zero.");
        }
    }
}
