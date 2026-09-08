namespace NeverOrder.Domain.Orders;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public OrderItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName;
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        Quantity = quantity;
        LineTotal = decimal.Round(UnitPrice * quantity, 2, MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>Snapshot: the catalog entry may later be renamed, repriced or removed.</summary>
    public string ProductName { get; private set; } = null!;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal { get; private set; }
}
