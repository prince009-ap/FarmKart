using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/customer/analytics")]
[Authorize(Roles = "Customer")]
public sealed class CustomerAnalyticsController(ICustomerAnalyticsService analyticsService) : ControllerBase
{
    private readonly ICustomerAnalyticsService _analyticsService = analyticsService;

    [HttpGet]
    public async Task<ActionResult<CustomerAnalyticsOverviewResponse>> GetCustomerAnalytics(
        [FromQuery] AnalyticsDateRange range = AnalyticsDateRange.Last30Days,
        [FromQuery] DateTime? customStartDateUtc = null,
        [FromQuery] DateTime? customEndDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var request = new AnalyticsDateRangeRequest(range, customStartDateUtc, customEndDateUtc);
        var response = await _analyticsService.GetCustomerAnalyticsAsync(userId, request, cancellationToken);
        return Ok(response);
    }
}
