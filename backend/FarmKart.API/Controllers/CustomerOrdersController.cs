using System.Security.Claims;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/customer/orders")]
[Authorize(Roles = Roles.Customer)]
public sealed class CustomerOrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? sortBy,
        CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var filter = new CustomerOrderFilterRequest(search, status, sortBy);
            var orders = await orderService.GetCustomerOrdersAsync(userId, filter, cancellationToken);
            return Ok(orders);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var order = await orderService.GetCustomerOrderDetailsAsync(userId, id, cancellationToken);
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> GetOrderTracking(Guid id, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var tracking = await orderService.GetCustomerOrderTrackingAsync(userId, id, cancellationToken);
            return Ok(tracking);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var order = await orderService.UpdateOrderStatusAsync(userId, id, request, cancellationToken);
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/fulfillment")]
    public async Task<IActionResult> UpdateFulfillmentDetails(
        Guid id,
        [FromBody] UpdateFulfillmentDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var order = await orderService.UpdateCustomerOrderFulfillmentAsync(userId, id, request, cancellationToken);
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
