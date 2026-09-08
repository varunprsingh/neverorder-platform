using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverOrder.Domain.Events;

namespace NeverOrder.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.EventId);

        builder.Property(m => m.EventType).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(1000);

        // The drain query: unpublished rows that are due.
        builder.HasIndex(m => new { m.ProcessedAt, m.NextAttemptAt });

        builder.Ignore(m => m.IsPublished);
    }
}

public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        // Composite key is the idempotency guard: the second insert of the same delivery fails.
        builder.HasKey(e => new { e.EventId, e.ConsumerName });

        builder.Property(e => e.ConsumerName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);

        builder.HasIndex(e => e.ProcessedAt);
    }
}

public sealed class DeadLetterEventConfiguration : IEntityTypeConfiguration<DeadLetterEvent>
{
    public void Configure(EntityTypeBuilder<DeadLetterEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.FailureReason).IsRequired().HasMaxLength(1000);

        builder.HasIndex(e => e.EventId);
        builder.HasIndex(e => e.LastFailedAt);
    }
}
