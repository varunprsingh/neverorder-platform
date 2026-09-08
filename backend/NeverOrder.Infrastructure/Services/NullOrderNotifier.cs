using NeverOrder.Application.Abstractions;

namespace NeverOrder.Infrastructure.Services;

/// <summary>
/// The default, so the domain can advance orders in a host that has no hub attached — a worker
/// process, or a test. The API replaces it with the SignalR implementation.
/// </summary>
public sealed class NullOrderNotifier : IOrderNotifier
{
    public Task OrderStatusChangedAsync(
        Guid userId,
        OrderStatusNotification notification,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
