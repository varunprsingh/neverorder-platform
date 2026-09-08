using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Abstractions;
using NeverOrder.Domain.Catalog;
using NeverOrder.Infrastructure.Identity;

namespace NeverOrder.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SeedOptions _options;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceScopeFactory scopeFactory,
        IOptions<SeedOptions> options,
        ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeverOrderDbContext>();

        if (_options.ApplyMigrations)
        {
            _logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(cancellationToken);
        }

        await SeedRolesAsync(scope.ServiceProvider, cancellationToken);
        await SeedAdminAsync(scope.ServiceProvider, cancellationToken);
        await SeedCatalogAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IClock>(),
            scope.ServiceProvider.GetRequiredService<ICacheService>(),
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedRolesAsync(IServiceProvider services, CancellationToken ct)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var role in Roles.All)
        {
            ct.ThrowIfCancellationRequested();

            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }
    }

    private async Task SeedAdminAsync(IServiceProvider services, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.AdminPassword))
        {
            _logger.LogWarning(
                "Seed:AdminPassword is not configured, so no administrator was created. Set it via user-secrets.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var clock = services.GetRequiredService<IClock>();

        if (await userManager.FindByEmailAsync(_options.AdminEmail) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = _options.AdminEmail,
            Email = _options.AdminEmail,
            EmailConfirmed = true,
            DisplayName = "NeverOrder Admin",
            CreatedAt = clock.UtcNow
        };

        var result = await userManager.CreateAsync(admin, _options.AdminPassword);
        if (!result.Succeeded)
        {
            _logger.LogError(
                "Could not create the seed administrator: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, new[] { Roles.User, Roles.Admin });
        _logger.LogInformation("Seeded administrator {Email}", _options.AdminEmail);
    }

    private async Task SeedCatalogAsync(NeverOrderDbContext db, IClock clock, ICacheService cache, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct))
        {
            return;
        }

        var now = clock.UtcNow;

        var categories = CatalogSeedData.Categories
            .Select(c => new Category(c.Name, c.Slug))
            .ToList();

        db.Categories.AddRange(categories);

        var bySlug = categories.ToDictionary(c => c.Slug);

        var products = CatalogSeedData.Products
            .Select(p => new Product(
                p.Name,
                p.Description,
                p.Price,
                bySlug[p.CategorySlug].Id,
                $"https://picsum.photos/seed/{Uri.EscapeDataString(p.ImageSeed)}/600/600",
                p.Quantity,
                now))
            .ToList();

        db.Products.AddRange(products);

        await db.SaveChangesAsync(ct);

        // A cache that outlived the previous seed would otherwise keep serving the old catalogue.
        await cache.InvalidateVersionAsync(CacheKeys.CatalogVersion, ct);

        _logger.LogInformation(
            "Seeded {CategoryCount} categories and {ProductCount} products",
            categories.Count, products.Count);
    }
}
