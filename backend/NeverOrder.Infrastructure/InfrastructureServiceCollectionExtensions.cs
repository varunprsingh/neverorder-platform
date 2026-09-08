using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Auth;
using NeverOrder.Application.Catalog;
using NeverOrder.Application.Messaging;
using NeverOrder.Application.Orders;
using NeverOrder.Infrastructure.Caching;
using NeverOrder.Infrastructure.Diagnostics;
using NeverOrder.Infrastructure.Identity;
using NeverOrder.Infrastructure.Messaging;
using NeverOrder.Infrastructure.Messaging.Consumers;
using NeverOrder.Infrastructure.Persistence;
using NeverOrder.Infrastructure.Services;
using NeverOrder.Infrastructure.Workers;

namespace NeverOrder.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Marks the probes that decide whether this instance can serve traffic.</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<CheckoutOptions>(configuration.GetSection(CheckoutOptions.SectionName));
        services.Configure<OrderSimulationOptions>(configuration.GetSection(OrderSimulationOptions.SectionName));
        services.Configure<RetryPolicyOptions>(configuration.GetSection(RetryPolicyOptions.SectionName));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        services.AddDbContext<NeverOrderDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<NeverOrderDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 10;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<NeverOrderDbContext>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());

        // Replaced by the API with the SignalR implementation; a host without a hub still advances orders.
        services.AddSingleton<IOrderNotifier, NullOrderNotifier>();

        // Every dependency registers its own readiness probe, so nothing can be added here and
        // forgotten there.
        services.AddHealthChecks()
            .AddDbContextCheck<NeverOrderDbContext>("postgres", tags: new[] { ReadyTag });

        AddCaching(services, configuration);
        AddMessaging(services, configuration);

        services.AddHostedService<OrderProgressionWorker>();
        services.AddHostedService<DatabaseInitializer>();

        return services;
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
        services.AddSingleton(options);
        services.Configure<CatalogCacheOptions>(configuration.GetSection(CatalogCacheOptions.SectionName));

        if (!options.Enabled)
        {
            services.AddSingleton<ICacheService, NullCacheService>();
            return;
        }

        if (options.UsesRedis)
        {
            services.AddStackExchangeRedisCache(redis =>
            {
                redis.Configuration = options.Redis;
                redis.InstanceName = options.InstanceName;
            });

            services.AddHealthChecks()
                .AddCheck<DistributedCacheHealthCheck>("redis", tags: new[] { ReadyTag });
        }
        else
        {
            // Same code path, one process. Correct for a single instance and for local runs without
            // a cache server; it simply stops being a shared cache once there is more than one.
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<ICacheService, DistributedCacheService>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();

        if (!options.Enabled)
        {
            services.AddSingleton<IEventPublisher, LoggingEventPublisher>();
            return;
        }

        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<RabbitMqTopology>();
        services.AddSingleton<IMessageTransport, RabbitMqMessageTransport>();

        services.AddHealthChecks()
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { ReadyTag });

        // Business code publishes into the outbox; only the drain worker touches the broker.
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddScoped<EventDispatcher>();

        services.AddHostedService<OutboxPublisherWorker>();
        services.AddHostedService<OrderEventsConsumer>();
        services.AddHostedService<DeadLetterConsumer>();
    }
}
