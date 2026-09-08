namespace NeverOrder.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// When false the app runs broker-free: events are logged instead of published and the
    /// scheduler takes over order confirmation. Useful for tests and Docker-less demos.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "neverorder";

    public string Password { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    public string Exchange { get; set; } = "neverorder.events";

    /// <summary>Holds messages waiting out their backoff; each bound queue has a TTL.</summary>
    public string RetryExchange { get; set; } = "neverorder.retry";

    /// <summary>Where expired retry messages land so they re-enter the main queue.</summary>
    public string RequeueExchange { get; set; } = "neverorder.requeue";

    public string DeadLetterExchange { get; set; } = "neverorder.dlx";

    public string OrdersQueue { get; set; } = "neverorder.orders";

    public string DeadLetterQueue { get; set; } = "neverorder.orders.dead";

    public string OrdersRoutingKey { get; set; } = "order.*";

    public string RequeueRoutingKey { get; set; } = "orders";

    public string DeadLetterRoutingKey { get; set; } = "orders.dead";

    public ushort PrefetchCount { get; set; } = 10;

    public string ClientProvidedName { get; set; } = "neverorder-api";

    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    public string RetryQueueName(int delaySeconds) => $"{OrdersQueue}.retry.{delaySeconds}s";

    public static string RetryRoutingKey(int delaySeconds) => $"retry.{delaySeconds}s";
}

public static class MessageHeaders
{
    public const string Attempt = "x-attempt";
    public const string FailureReason = "x-failure-reason";
    public const string OriginalRoutingKey = "x-original-routing-key";
}
