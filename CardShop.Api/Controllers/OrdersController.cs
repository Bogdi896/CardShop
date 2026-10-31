
using System.IdentityModel.Tokens.Jwt;
using CardShop.Application.DTOs.Orders;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Common;
using CardShop.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private string? GetUserId()
    {
        return User.FindFirst(
            JwtRegisteredClaimNames.Sub)?.Value;
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<OrderResponse>> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var order = await _orderService.CheckoutAsync(
                userId,
                request,
                cancellationToken);

            return Ok(order);
        }
        catch (OrderException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponse>>> Search(
        [FromQuery] SearchOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _orderService.SearchAsync(
            query,
            userId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var order = await _orderService.GetByIdAsync(
            id,
            userId,
            cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        return Ok(order);
    }
}
