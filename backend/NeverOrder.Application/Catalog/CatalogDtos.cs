namespace NeverOrder.Application.Catalog;

public sealed record CategoryDto(Guid Id, string Name, string Slug);

public sealed record ProductSummaryDto(
    Guid Id,
    string Name,
    decimal Price,
    string ImageUrl,
    string CategoryName,
    string CategorySlug,
    int AvailableQuantity,
    bool InStock);

public sealed record ProductDetailDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string ImageUrl,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    int AvailableQuantity,
    bool InStock);

public sealed record ProductQuery(
    string? Search = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? InStock = null,
    string? Sort = null,
    int? Page = null,
    int? PageSize = null);
