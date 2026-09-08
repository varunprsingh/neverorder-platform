using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NeverOrder.Application.Carts;
using NeverOrder.Application.Catalog;
using NeverOrder.Application.Orders;

namespace NeverOrder.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CatalogService>();
        services.AddScoped<CartService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderQueryService>();
        services.AddScoped<OrderProgressionService>();

        services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>(ServiceLifetime.Scoped);

        return services;
    }
}

public sealed class ApplicationAssemblyMarker;
