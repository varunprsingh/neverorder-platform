using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverOrder.Application.Common;
using NeverOrder.Application.Orders;

namespace NeverOrder.Api.Controllers;

[Authorize]
[Route("api/orders")]
public sealed class OrdersController : ApiControllerBase
{
    private readonly CheckoutService _checkout;
    private readonly OrderQueryService _orders;

    public OrdersController(CheckoutService checkout, OrderQueryService orders)
    {
        _checkout = checkout;
        _orders = orders;
    }

    /// <summary>
    /// Places a virtual order. Accepts no prices: totals are computed from the live catalog.
    /// Returns immediately; state changes happen asynchronously in the background.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderPlacedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderPlacedDto>> Checkout(CancellationToken ct)
    {
        var order = await _checkout.CheckoutAsync(CurrentUserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetHistory(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct) =>
        Ok(await _orders.GetHistoryAsync(CurrentUserId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _orders.GetByIdAsync(CurrentUserId, id, ct));

    [HttpGet("{id:guid}/tracking")]
    [ProducesResponseType(typeof(OrderTrackingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderTrackingDto>> GetTracking(Guid id, CancellationToken ct) =>
        Ok(await _orders.GetTrackingAsync(CurrentUserId, id, ct));
}
