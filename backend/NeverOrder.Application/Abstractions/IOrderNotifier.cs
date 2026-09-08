namespace NeverOrder.Application.Abstractions;

/// <summary>Exactly what a tracking view needs to refresh itself, and nothing more.</summary>
public sealed record OrderStatusNotification(
    Guid OrderId,
    string OrderNumber,
    string FromStatus,
    string Status,
    DateTimeOffset OccurredAt,
    bool IsComplete);

/// <summary>
/// Announces a transition to whoever is watching it. The owner of the transition raises this after the
/// change has committed, so a delivered notification always describes state that really exists.
/// </summary>
public interface IOrderNotifier
{
    /// <summary>The user id is routing, not payload: it decides who is told, and is never sent.</summary>
    Task OrderStatusChangedAsync(
        Guid userId,
        OrderStatusNotification notification,
        CancellationToken cancellationToken = default);
}
