using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NeverOrder.Domain.Carts;
using NeverOrder.Domain.Catalog;
using NeverOrder.Domain.Events;
using NeverOrder.Domain.Orders;

namespace NeverOrder.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Product> Products { get; }

    DbSet<Category> Categories { get; }

    DbSet<Cart> Carts { get; }

    DbSet<Order> Orders { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<ProcessedEvent> ProcessedEvents { get; }

    DbSet<DeadLetterEvent> DeadLetterEvents { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
