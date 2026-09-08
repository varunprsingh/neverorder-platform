using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Common;
using NeverOrder.Domain.Catalog;

namespace NeverOrder.Application.Catalog;

public sealed class CatalogCacheOptions
{
    public const string SectionName = "Cache:Catalog";

    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(5);
}

public sealed class CatalogService
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly CatalogCacheOptions _options;

    public CatalogService(IApplicationDbContext db, ICacheService cache, IOptions<CatalogCacheOptions> options)
    {
        _db = db;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<PagedResult<ProductSummaryDto>> SearchAsync(ProductQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var version = await _cache.GetVersionAsync(CacheKeys.CatalogVersion, ct);
        var cacheKey = CacheKeys.Products(version, Fingerprint(query, page, pageSize));

        var cached = await _cache.GetAsync<PagedResult<ProductSummaryDto>>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        var result = await QueryAsync(query, page, pageSize, ct);
        await _cache.SetAsync(cacheKey, result, _options.TimeToLive, ct);

        return result;
    }

    private async Task<PagedResult<ProductSummaryDto>> QueryAsync(
        ProductQuery query,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var products = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Parameterised by EF; lowered on both sides so the match is case-insensitive.
            var term = query.Search.Trim().ToLowerInvariant();
            products = products.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Description.ToLower().Contains(term) ||
                p.Category.Name.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var slug = query.Category.Trim().ToLowerInvariant();
            products = products.Where(p => p.Category.Slug == slug);
        }

        if (query.MinPrice is { } min)
        {
            products = products.Where(p => p.Price >= min);
        }

        if (query.MaxPrice is { } max)
        {
            products = products.Where(p => p.Price <= max);
        }

        if (query.InStock == true)
        {
            products = products.Where(p => p.AvailableQuantity > 0);
        }

        products = ApplySort(products, query.Sort);

        var totalCount = await products.CountAsync(ct);

        var items = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductSummaryDto(
                p.Id,
                p.Name,
                p.Price,
                p.ImageUrl,
                p.Category.Name,
                p.Category.Slug,
                p.AvailableQuantity,
                p.AvailableQuantity > 0))
            .ToListAsync(ct);

        return new PagedResult<ProductSummaryDto>(items, page, pageSize, totalCount);
    }

    public async Task<ProductDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.IsActive)
            .Select(p => new ProductDetailDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.ImageUrl,
                p.CategoryId,
                p.Category.Name,
                p.Category.Slug,
                p.AvailableQuantity,
                p.AvailableQuantity > 0))
            .SingleOrDefaultAsync(ct);

        return product ?? throw new NotFoundException("Product");
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var version = await _cache.GetVersionAsync(CacheKeys.CatalogVersion, ct);
        var cacheKey = CacheKeys.Categories(version);

        var cached = await _cache.GetAsync<List<CategoryDto>>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug))
            .ToListAsync(ct);

        await _cache.SetAsync(cacheKey, categories, _options.TimeToLive, ct);

        return categories;
    }

    /// <summary>Every field that changes the result set has to be in the key, or a filter would
    /// silently serve another filter's page.</summary>
    private static string Fingerprint(ProductQuery query, int page, int pageSize) => string.Join(
        '|',
        query.Search?.Trim().ToLowerInvariant() ?? string.Empty,
        query.Category?.Trim().ToLowerInvariant() ?? string.Empty,
        query.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        query.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        query.InStock?.ToString() ?? string.Empty,
        query.Sort ?? string.Empty,
        page.ToString(CultureInfo.InvariantCulture),
        pageSize.ToString(CultureInfo.InvariantCulture));

    private static IQueryable<Product> ApplySort(IQueryable<Product> products, string? sort) => sort switch
    {
        "price_asc" => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
        "price_desc" => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
        "newest" => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
        _ => products.OrderBy(p => p.Name).ThenBy(p => p.Id)
    };
}
