
using CardShop.Application.Common;
using CardShop.Application.Common.Constants;
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Orders;
using CardShop.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardShop.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("admin/orders")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public AdminOrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponse>>> Search(
        [FromQuery] SearchOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.SearchAsync(
            query,
            userId: null,
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetByIdAsync(
            id,
            userId: null,
            cancellationToken: cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPut("{id:int}/ship")]
    public async Task<IActionResult> Ship(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _orderService.ShipAsync(
                id,
                cancellationToken);

            return found ? NoContent() : NotFound();
        }
        catch (OrderException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var found = await _orderService.CancelAsync(
                id,
                cancellationToken);

            return found ? NoContent() : NotFound();
        }
        catch (OrderException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
