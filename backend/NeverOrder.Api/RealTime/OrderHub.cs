using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace NeverOrder.Api.RealTime;

/// <summary>
/// Clients only listen. There is no method to subscribe to an order, because a client asking to watch
/// an id is a client that can ask to watch someone else's: membership is derived from the token alone.
/// </summary>
[Authorize]
public sealed class OrderHub : Hub
{
    public const string Path = "/hubs/orders";

    public const string OrderStatusChanged = "orderStatusChanged";

    private readonly ILogger<OrderHub> _logger;

    public OrderHub(ILogger<OrderHub> logger) => _logger = logger;

    /// <summary>One group per user, so a connection can only ever be reached through its own id.</summary>
    public static string GroupFor(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = UserId();

        if (userId is null)
        {
            // Authorized but without a usable subject claim: nothing can be routed to it.
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId.Value));
        _logger.LogDebug("Connection {ConnectionId} joined {Group}", Context.ConnectionId, GroupFor(userId.Value));

        await base.OnConnectedAsync();
    }

    // Leaving the group on disconnect is deliberate housekeeping only: SignalR already drops group
    // membership with the connection.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (UserId() is { } userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(userId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid? UserId()
    {
        var value = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
