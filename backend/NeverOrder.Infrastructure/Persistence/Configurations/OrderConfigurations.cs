using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverOrder.Domain.Orders;
using NeverOrder.Infrastructure.Identity;

namespace NeverOrder.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.HasIndex(o => o.OrderNumber).IsUnique();

        // Stored as text so the database stays readable during demos and debugging.
        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(o => o.Subtotal).HasPrecision(18, 2);
        builder.Property(o => o.DeliveryFee).HasPrecision(18, 2);
        builder.Property(o => o.Total).HasPrecision(18, 2);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Order.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(Order.StatusHistory))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => new { o.UserId, o.CreatedAt });

        // Hot path for the progression worker: "which orders are due right now?"
        builder.HasIndex(o => new { o.Status, o.NextTransitionAt });

        // PostgreSQL stamps every row with the id of the transaction that last wrote it. Mapping that
        // system column costs no schema and makes a second writer's UPDATE match zero rows, so two
        // API instances cannot advance the same order twice.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.LineTotal).HasPrecision(18, 2);

        builder.HasIndex(i => i.OrderId);
    }
}

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(h => h.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(h => h.Note).HasMaxLength(500);

        builder.HasIndex(h => new { h.OrderId, h.OccurredAt });
    }
}
