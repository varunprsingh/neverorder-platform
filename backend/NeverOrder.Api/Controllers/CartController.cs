using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverOrder.Application.Carts;

namespace NeverOrder.Api.Controllers;

public sealed record AddCartItemRequest(Guid ProductId, int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);

[Authorize]
[Route("api/cart")]
public sealed class CartController : ApiControllerBase
{
    private readonly CartService _cart;

    public CartController(CartService cart) => _cart = cart;

    [HttpGet]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct) =>
        Ok(await _cart.GetAsync(CurrentUserId, ct));

    [HttpPost("items")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartDto>> AddItem(AddCartItemRequest request, CancellationToken ct) =>
        Ok(await _cart.AddItemAsync(CurrentUserId, request.ProductId, request.Quantity, ct));

    [HttpPut("items/{productId:guid}")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartDto>> UpdateItem(
        Guid productId,
        UpdateCartItemRequest request,
        CancellationToken ct) =>
        Ok(await _cart.UpdateItemAsync(CurrentUserId, productId, request.Quantity, ct));

    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartDto>> RemoveItem(Guid productId, CancellationToken ct) =>
        Ok(await _cart.RemoveItemAsync(CurrentUserId, productId, ct));

    [HttpDelete]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Clear(CancellationToken ct) =>
        Ok(await _cart.ClearAsync(CurrentUserId, ct));
}
