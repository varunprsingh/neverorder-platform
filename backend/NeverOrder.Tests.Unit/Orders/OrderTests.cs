using NeverOrder.Domain.Orders;

namespace NeverOrder.Tests.Unit.Orders;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Order NewOrder(params OrderItem[] items) =>
        Order.Create(Guid.NewGuid(), "NO-20260101-120000-ABCD", items, deliveryFee: 0m, Now);

    private static OrderItem Item(decimal price, int quantity) =>
        new(Guid.NewGuid(), "Test product", price, quantity);

    [Fact]
    public void Create_sums_line_totals_into_the_order_total()
    {
        var order = NewOrder(Item(1299.50m, 2), Item(499.99m, 3));

        order.Subtotal.Should().Be(4098.97m);
        order.DeliveryFee.Should().Be(0m);
        order.Total.Should().Be(4098.97m);
    }

    [Fact]
    public void Create_adds_the_delivery_fee_to_the_total()
    {
        var order = Order.Create(Guid.NewGuid(), "NO-1", new[] { Item(100m, 1) }, deliveryFee: 49m, Now);

        order.Total.Should().Be(149m);
    }

    [Fact]
    public void Create_starts_in_Created_with_an_opening_history_entry()
    {
        var order = NewOrder(Item(100m, 1));

        order.Status.Should().Be(OrderStatus.Created);
        order.StatusHistory.Should().ContainSingle();
        order.StatusHistory[0].FromStatus.Should().BeNull();
        order.StatusHistory[0].ToStatus.Should().Be(OrderStatus.Created);
    }

    [Fact]
    public void Create_rejects_an_empty_order()
    {
        var act = () => NewOrder();

        act.Should().Throw<EmptyOrderException>();
    }

    [Fact]
    public void TransitionTo_records_each_move_in_history()
    {
        var order = NewOrder(Item(100m, 1));

        order.TransitionTo(OrderStatus.Confirmed, Now.AddSeconds(2));
        order.TransitionTo(OrderStatus.Preparing, Now.AddSeconds(7));

        order.Status.Should().Be(OrderStatus.Preparing);
        order.StatusHistory.Should().HaveCount(3);
        order.StatusHistory[2].FromStatus.Should().Be(OrderStatus.Confirmed);
        order.StatusHistory[2].ToStatus.Should().Be(OrderStatus.Preparing);
    }

    [Fact]
    public void TransitionTo_rejects_skipping_ahead()
    {
        var order = NewOrder(Item(100m, 1));

        var act = () => order.TransitionTo(OrderStatus.Delivered, Now);

        act.Should().Throw<InvalidOrderTransitionException>();
        order.Status.Should().Be(OrderStatus.Created);
        order.StatusHistory.Should().ContainSingle("a rejected transition must not be recorded");
    }

    [Fact]
    public void Reaching_Delivered_clears_the_next_transition_time()
    {
        var order = NewOrder(Item(100m, 1));
        order.ScheduleNextTransition(Now.AddSeconds(2));

        order.TransitionTo(OrderStatus.Confirmed, Now.AddSeconds(2));
        order.TransitionTo(OrderStatus.Preparing, Now.AddSeconds(7));
        order.TransitionTo(OrderStatus.OutForDelivery, Now.AddSeconds(12));
        order.TransitionTo(OrderStatus.Delivered, Now.AddSeconds(20));

        order.Status.Should().Be(OrderStatus.Delivered);
        order.NextTransitionAt.Should().BeNull();
    }

    [Fact]
    public void ScheduleNextTransition_is_ignored_once_terminal()
    {
        var order = NewOrder(Item(100m, 1));
        order.TransitionTo(OrderStatus.Confirmed, Now);
        order.TransitionTo(OrderStatus.Preparing, Now);
        order.TransitionTo(OrderStatus.OutForDelivery, Now);
        order.TransitionTo(OrderStatus.Delivered, Now);

        order.ScheduleNextTransition(Now.AddSeconds(5));

        order.NextTransitionAt.Should().BeNull();
    }

    [Fact]
    public void OrderItem_rejects_a_non_positive_quantity()
    {
        var act = () => Item(100m, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
