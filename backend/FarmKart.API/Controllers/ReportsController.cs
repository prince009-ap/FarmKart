using System.Security.Claims;
using FarmKart.Application.Abstractions.Report;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var report = await reportService.CreateReportAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetReportById), new { id = report.Id }, report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetUserReports([FromQuery] ReportQueryRequest request, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await reportService.GetUserReportsAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetReportById(Guid id, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var report = await reportService.GetReportByIdAsync(userId, id, cancellationToken);
        if (report == null)
        {
            return NotFound(new { message = "Report not found." });
        }

        return Ok(report);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
