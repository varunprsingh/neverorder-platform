using NeverOrder.Domain.Orders;

namespace NeverOrder.Tests.Unit.Orders;

public class OrderStateMachineTests
{
    public static TheoryData<OrderStatus, OrderStatus> LegalTransitions => new()
    {
        { OrderStatus.Created, OrderStatus.Confirmed },
        { OrderStatus.Confirmed, OrderStatus.Preparing },
        { OrderStatus.Preparing, OrderStatus.OutForDelivery },
        { OrderStatus.OutForDelivery, OrderStatus.Delivered },
        { OrderStatus.Created, OrderStatus.Failed },
        { OrderStatus.Confirmed, OrderStatus.Failed },
        { OrderStatus.Preparing, OrderStatus.Failed },
        { OrderStatus.OutForDelivery, OrderStatus.Failed }
    };

    public static TheoryData<OrderStatus, OrderStatus> IllegalTransitions => new()
    {
        { OrderStatus.Created, OrderStatus.Preparing },
        { OrderStatus.Created, OrderStatus.OutForDelivery },
        { OrderStatus.Created, OrderStatus.Delivered },
        { OrderStatus.Confirmed, OrderStatus.Created },
        { OrderStatus.Confirmed, OrderStatus.OutForDelivery },
        { OrderStatus.Confirmed, OrderStatus.Delivered },
        { OrderStatus.Preparing, OrderStatus.Created },
        { OrderStatus.Preparing, OrderStatus.Confirmed },
        { OrderStatus.Preparing, OrderStatus.Delivered },
        { OrderStatus.OutForDelivery, OrderStatus.Preparing },
        { OrderStatus.Delivered, OrderStatus.Preparing },
        { OrderStatus.Delivered, OrderStatus.OutForDelivery },
        { OrderStatus.Delivered, OrderStatus.Failed },
        { OrderStatus.Failed, OrderStatus.Confirmed }
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public void CanTransition_allows_forward_moves(OrderStatus from, OrderStatus to) =>
        OrderStateMachine.CanTransition(from, to).Should().BeTrue();

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public void CanTransition_rejects_skips_and_reversals(OrderStatus from, OrderStatus to) =>
        OrderStateMachine.CanTransition(from, to).Should().BeFalse();

    [Theory]
    [InlineData(OrderStatus.Created, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Preparing, OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Delivered)]
    public void NextOnHappyPath_walks_the_simulation(OrderStatus from, OrderStatus expected) =>
        OrderStateMachine.NextOnHappyPath(from).Should().Be(expected);

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Failed)]
    public void NextOnHappyPath_stops_at_terminal_states(OrderStatus status) =>
        OrderStateMachine.NextOnHappyPath(status).Should().BeNull();

    [Theory]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Failed, true)]
    [InlineData(OrderStatus.Created, false)]
    [InlineData(OrderStatus.OutForDelivery, false)]
    public void IsTerminal_identifies_end_states(OrderStatus status, bool expected) =>
        OrderStateMachine.IsTerminal(status).Should().Be(expected);

    [Fact]
    public void No_status_may_transition_to_itself()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            OrderStateMachine.CanTransition(status, status).Should().BeFalse($"{status} should not loop to itself");
        }
    }

    [Fact]
    public void Every_status_is_covered_by_the_transition_table()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            var act = () => OrderStateMachine.AllowedFrom(status);
            act.Should().NotThrow($"{status} must have an entry so new states cannot be forgotten");
        }
    }
}
