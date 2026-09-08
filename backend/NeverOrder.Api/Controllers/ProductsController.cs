using Microsoft.AspNetCore.Mvc;
using NeverOrder.Application.Catalog;
using NeverOrder.Application.Common;

namespace NeverOrder.Api.Controllers;

[Route("api/products")]
public sealed class ProductsController : ApiControllerBase
{
    private readonly CatalogService _catalog;

    public ProductsController(CatalogService catalog) => _catalog = catalog;

    /// <summary>Browsing is open to guests; only active products are returned.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductSummaryDto>>> Search(
        [FromQuery] ProductQuery query,
        CancellationToken ct) =>
        Ok(await _catalog.SearchAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _catalog.GetByIdAsync(id, ct));
}

[Route("api/categories")]
public sealed class CategoriesController : ApiControllerBase
{
    private readonly CatalogService _catalog;

    public CategoriesController(CatalogService catalog) => _catalog = catalog;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct) =>
        Ok(await _catalog.GetCategoriesAsync(ct));
}
