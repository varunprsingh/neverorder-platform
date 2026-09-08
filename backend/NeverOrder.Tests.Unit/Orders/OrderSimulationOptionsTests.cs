using NeverOrder.Application.Orders;
using NeverOrder.Domain.Orders;

namespace NeverOrder.Tests.Unit.Orders;

public class OrderSimulationOptionsTests
{
    private static readonly OrderSimulationOptions Options = new()
    {
        CreatedToConfirmed = TimeSpan.FromSeconds(2),
        ConfirmedToPreparing = TimeSpan.FromSeconds(5),
        PreparingToOutForDelivery = TimeSpan.FromSeconds(6),
        OutForDeliveryToDelivered = TimeSpan.FromSeconds(8)
    };

    [Theory]
    [InlineData(OrderStatus.Created, 2)]
    [InlineData(OrderStatus.Confirmed, 5)]
    [InlineData(OrderStatus.Preparing, 6)]
    [InlineData(OrderStatus.OutForDelivery, 8)]
    public void DelayAfter_maps_each_stage_to_its_configured_wait(OrderStatus status, int expectedSeconds) =>
        Options.DelayAfter(status).Should().Be(TimeSpan.FromSeconds(expectedSeconds));

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Failed)]
    public void DelayAfter_returns_null_for_terminal_states(OrderStatus status) =>
        Options.DelayAfter(status).Should().BeNull();

    [Fact]
    public void Every_non_terminal_status_has_a_configured_delay()
    {
        foreach (var status in Enum.GetValues<OrderStatus>().Where(s => !OrderStateMachine.IsTerminal(s)))
        {
            Options.DelayAfter(status).Should().NotBeNull($"{status} must know how long to wait");
        }
    }
}
