using Microsoft.AspNetCore.SignalR;
using NeverOrder.Api.RealTime;
using NeverOrder.Application.Abstractions;
using NSubstitute;

namespace NeverOrder.Tests.Integration.RealTime;

public sealed class OrderNotificationTests
{
    private readonly IClientProxy _proxy = Substitute.For<IClientProxy>();
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly IHubContext<OrderHub> _hub = Substitute.For<IHubContext<OrderHub>>();

    public OrderNotificationTests()
    {
        _clients.Group(Arg.Any<string>()).Returns(_proxy);
        _hub.Clients.Returns(_clients);
    }

    [Fact]
    public async Task A_notification_is_addressed_to_its_own_user_group()
    {
        var userId = Guid.NewGuid();
        var notification = Notification();

        await new SignalROrderNotifier(_hub).OrderStatusChangedAsync(userId, notification);

        _clients.Received(1).Group($"user:{userId}");

        await _proxy.Received(1).SendCoreAsync(
            OrderHub.OrderStatusChanged,
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], notification)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_other_user_group_is_ever_addressed()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        await new SignalROrderNotifier(_hub).OrderStatusChangedAsync(owner, Notification());

        _clients.DidNotReceive().Group(OrderHub.GroupFor(stranger));
        _ = _clients.DidNotReceive().All;
    }

    [Fact]
    public void Group_names_are_distinct_per_user()
    {
        OrderHub.GroupFor(Guid.NewGuid()).Should().NotBe(OrderHub.GroupFor(Guid.NewGuid()));
    }

    /// <summary>
    /// Routing is the user id's only job here. If it ever became payload, every client watching an
    /// order would learn who placed it.
    /// </summary>
    [Fact]
    public void The_payload_carries_no_user_identity()
    {
        var names = typeof(OrderStatusNotification)
            .GetProperties()
            .Select(property => property.Name);

        names.Should().NotContain(name => name.Contains("User", StringComparison.OrdinalIgnoreCase));
    }

    private static OrderStatusNotification Notification() => new(
        Guid.NewGuid(),
        "NO-20260907-000000-AAAA",
        "Created",
        "Confirmed",
        DateTimeOffset.UtcNow,
        IsComplete: false);
}
