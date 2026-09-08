using NeverOrder.Domain.Common;

namespace NeverOrder.Domain.Catalog;

public sealed class ProductUnavailableException : DomainException
{
    public ProductUnavailableException(string message) : base(message)
    {
    }
}

public sealed class Product
{
    private Product()
    {
    }

    public Product(
        string name,
        string description,
        decimal price,
        Guid categoryId,
        string imageUrl,
        int availableQuantity,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        SetPrice(price, now);
        CategoryId = categoryId;
        ImageUrl = imageUrl;
        SetQuantity(availableQuantity, now);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public decimal Price { get; private set; }

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public string ImageUrl { get; private set; } = null!;

    public int AvailableQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsOrderable => IsActive && AvailableQuantity > 0;

    public void SetPrice(decimal price, DateTimeOffset now)
    {
        if (price < 0)
        {
            throw new ProductUnavailableException("Price cannot be negative.");
        }

        Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        UpdatedAt = now;
    }

    public void SetQuantity(int quantity, DateTimeOffset now)
    {
        if (quantity < 0)
        {
            throw new ProductUnavailableException("Available quantity cannot be negative.");
        }

        AvailableQuantity = quantity;
        UpdatedAt = now;
    }

    public void UpdateDetails(string name, string description, Guid categoryId, string imageUrl, DateTimeOffset now)
    {
        Name = name;
        Description = description;
        CategoryId = categoryId;
        ImageUrl = imageUrl;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
    }
}
