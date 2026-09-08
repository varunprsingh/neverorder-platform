using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeverOrder.Infrastructure.Messaging;

/// <summary>The wire format for every event, per the shared event model.</summary>
public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    Guid AggregateId,
    string? CorrelationId,
    JsonElement Payload);

public static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
