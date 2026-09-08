using NeverOrder.Domain.Carts;

namespace NeverOrder.Tests.Unit.Carts;

public class CartTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ProductId = Guid.NewGuid();

    private static Cart NewCart() => new(Guid.NewGuid(), Now);

    [Fact]
    public void A_new_cart_is_empty()
    {
        var cart = NewCart();

        cart.IsEmpty.Should().BeTrue();
        cart.Subtotal.Should().Be(0m);
    }

    [Fact]
    public void Adding_the_same_product_twice_increases_quantity_instead_of_duplicating()
    {
        var cart = NewCart();

        cart.AddOrIncrease(ProductId, "Headphones", 1000m, 1, Now);
        cart.AddOrIncrease(ProductId, "Headphones", 1000m, 2, Now);

        cart.Items.Should().ContainSingle();
        cart.Items[0].Quantity.Should().Be(3);
        cart.Subtotal.Should().Be(3000m);
    }

    [Fact]
    public void Adding_refreshes_the_price_snapshot()
    {
        var cart = NewCart();

        cart.AddOrIncrease(ProductId, "Headphones", 1000m, 1, Now);
        cart.AddOrIncrease(ProductId, "Headphones", 1200m, 1, Now);

        cart.Items[0].UnitPrice.Should().Be(1200m);
    }

    [Fact]
    public void Subtotal_sums_every_line()
    {
        var cart = NewCart();

        cart.AddOrIncrease(Guid.NewGuid(), "A", 1299.50m, 2, Now);
        cart.AddOrIncrease(Guid.NewGuid(), "B", 99.99m, 3, Now);

        cart.Subtotal.Should().Be(2898.97m);
    }

    [Fact]
    public void SetQuantity_replaces_rather_than_adds()
    {
        var cart = NewCart();
        cart.AddOrIncrease(ProductId, "Headphones", 1000m, 5, Now);

        cart.SetQuantity(ProductId, 2, Now);

        cart.Items[0].Quantity.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Quantities_must_be_positive(int quantity)
    {
        var cart = NewCart();

        var act = () => cart.AddOrIncrease(ProductId, "Headphones", 1000m, quantity, Now);

        act.Should().Throw<InvalidCartOperationException>();
    }

    [Fact]
    public void Removing_a_product_that_is_not_in_the_cart_fails()
    {
        var cart = NewCart();

        var act = () => cart.Remove(ProductId, Now);

        act.Should().Throw<InvalidCartOperationException>();
    }

    [Fact]
    public void Clear_empties_the_cart()
    {
        var cart = NewCart();
        cart.AddOrIncrease(ProductId, "Headphones", 1000m, 2, Now);

        cart.Clear(Now);

        cart.IsEmpty.Should().BeTrue();
        cart.Subtotal.Should().Be(0m);
    }
}
