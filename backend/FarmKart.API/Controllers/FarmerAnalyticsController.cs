using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/farmer/analytics")]
[Authorize(Roles = "Farmer")]
public sealed class FarmerAnalyticsController(IFarmerAnalyticsService analyticsService) : ControllerBase
{
    private readonly IFarmerAnalyticsService _analyticsService = analyticsService;

    [HttpGet]
    public async Task<ActionResult<FarmerAnalyticsOverviewResponse>> GetFarmerAnalytics(
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
        var response = await _analyticsService.GetFarmerAnalyticsAsync(userId, request, cancellationToken);
        return Ok(response);
    }
}
