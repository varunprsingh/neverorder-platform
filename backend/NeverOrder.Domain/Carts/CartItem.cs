namespace NeverOrder.Domain.Carts;

public sealed class CartItem
{
    private CartItem()
    {
    }

    internal CartItem(Guid productId, string productName, decimal unitPrice, int quantity, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName;
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        Quantity = quantity;
        AddedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid CartId { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>Snapshot for display; checkout always re-reads the live catalog.</summary>
    public string ProductName { get; private set; } = null!;

    /// <summary>Price when the item was added. Never trusted as the order price.</summary>
    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public decimal LineTotal => decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);

    internal void SetQuantity(int quantity) => Quantity = quantity;

    internal void Increase(int by) => Quantity += by;

    internal void RefreshSnapshot(string productName, decimal unitPrice)
    {
        ProductName = productName;
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
    }
}
