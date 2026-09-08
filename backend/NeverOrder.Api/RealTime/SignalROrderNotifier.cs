using Microsoft.AspNetCore.SignalR;
using NeverOrder.Application.Abstractions;

namespace NeverOrder.Api.RealTime;

public sealed class SignalROrderNotifier : IOrderNotifier
{
    private readonly IHubContext<OrderHub> _hub;

    public SignalROrderNotifier(IHubContext<OrderHub> hub) => _hub = hub;

    public Task OrderStatusChangedAsync(
        Guid userId,
        OrderStatusNotification notification,
        CancellationToken cancellationToken = default) =>
        _hub.Clients
            .Group(OrderHub.GroupFor(userId))
            .SendAsync(OrderHub.OrderStatusChanged, notification, cancellationToken);
}
