
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Carts;
using CardShop.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CardShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("cart")]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    private string? GetUserId()
    {
        return User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    }

    [HttpGet]
    public async Task<ActionResult<CartResponse>> GetCart(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            var cart = await _cartService.GetCartAsync(
                userId,
                cancellationToken);

            return Ok(cart);
        }
        catch (CartException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            var cart = await _cartService.AddItemAsync(
                userId,
                request,
                cancellationToken);

            return Ok(cart);
        }
        catch (CartException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("items/{productId:int}")]
    public async Task<ActionResult<CartResponse>> UpdateItem(
        int productId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            var cart = await _cartService.UpdateItemAsync(
                userId,
                productId,
                request,
                cancellationToken);

            return Ok(cart);
        }
        catch (CartException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<IActionResult> RemoveItem(
        int productId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            var removed = await _cartService.RemoveItemAsync(
                userId,
                productId,
                cancellationToken);

            if (!removed)
                return NotFound();

            return NoContent();
        }
        catch (CartException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            await _cartService.ClearCartAsync(
                userId,
                cancellationToken);

            return NoContent();
        }
        catch (CartException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
