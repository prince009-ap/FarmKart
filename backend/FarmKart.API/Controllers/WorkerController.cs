using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.Abstractions.Worker;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/worker")]
[Authorize(Roles = Roles.Worker)]
public class WorkerController : ControllerBase
{
    private readonly IWorkerJobService _workerJobService;
    private readonly IWorkerAssignmentService _workerAssignmentService;
    private readonly IWorkerAttendanceService _workerAttendanceService;
    private readonly IWorkerProfileService _workerProfileService;
    private readonly INotificationService _notificationService;
    private readonly IWorkerReviewService _workerReviewService;
    private readonly IWorkerEarningsService _workerEarningsService;
    private readonly IWorkerWorkHistoryService _workerWorkHistoryService;
    private readonly IWorkerProfileCompletionService _workerProfileCompletionService;

    public WorkerController(
        IWorkerJobService workerJobService,
        IWorkerAssignmentService workerAssignmentService,
        IWorkerAttendanceService workerAttendanceService,
        IWorkerProfileService workerProfileService,
        INotificationService notificationService,
        IWorkerReviewService workerReviewService,
        IWorkerEarningsService workerEarningsService,
        IWorkerWorkHistoryService workerWorkHistoryService,
        IWorkerProfileCompletionService workerProfileCompletionService)
    {
        _workerJobService = workerJobService;
        _workerAssignmentService = workerAssignmentService;
        _workerAttendanceService = workerAttendanceService;
        _workerProfileService = workerProfileService;
        _notificationService = notificationService;
        _workerReviewService = workerReviewService;
        _workerEarningsService = workerEarningsService;
        _workerWorkHistoryService = workerWorkHistoryService;
        _workerProfileCompletionService = workerProfileCompletionService;
    }

    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerProfileService.GetProfileAsync(userId.Value));
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] WorkerProfileUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerProfileService.UpdateProfileAsync(userId.Value, request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("profile/image")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadProfileImage(IFormFile file, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Uploaded file is empty." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var profile = await _workerProfileService.UploadProfileImageAsync(
                userId.Value, stream, file.FileName, file.ContentType, file.Length, cancellationToken);

            return Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("profile/image")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProfileImage(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var profile = await _workerProfileService.RemoveProfileImageAsync(userId.Value, cancellationToken);
            return Ok(profile);
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("profile/completion")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerProfileCompletionResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileCompletion()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerProfileCompletionService.GetProfileCompletionAsync(userId.Value));
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerPreferencesResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerProfileService.GetPreferencesAsync(userId.Value));
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerPreferencesResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePreferences([FromBody] WorkerPreferencesUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerProfileService.UpdatePreferencesAsync(userId.Value, request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("reviews")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerRatingSummaryResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyReviews()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerReviewService.GetWorkerRatingSummaryAsync(userId.Value));
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("earnings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerEarningsSummaryResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyEarnings()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerEarningsService.GetWorkerEarningsAsync(userId.Value));
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("work-history")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerWorkHistorySummaryResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyWorkHistory()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerWorkHistoryService.GetWorkerWorkHistoryAsync(userId.Value));
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("notifications")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<WorkerNotificationResponse>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _notificationService.GetWorkerNotificationsAsync(userId.Value));
    }

    [HttpGet("notifications/unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UnreadNotificationCountResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUnreadNotificationCount()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _notificationService.GetUnreadCountAsync(userId.Value));
    }

    [HttpPut("notifications/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerNotificationResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNotificationAsRead(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _notificationService.MarkAsReadAsync(userId.Value, id));
        }
        catch (JobNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("notifications/read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAllNotificationsAsRead()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        await _notificationService.MarkAllAsReadAsync(userId.Value);
        return Ok(new { message = "All notifications marked as read." });
    }

    [HttpGet("jobs")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<WorkerAvailableJobResponse>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetJobs()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _workerJobService.GetAvailableJobsAsync(userId.Value));
    }

    [HttpGet("jobs/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerAvailableJobResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerJobService.GetAvailableJobDetailsAsync(userId.Value, id));
        }
        catch (JobNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("jobs/{id:guid}/apply")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerJobApplicationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyToJob(Guid id, [FromBody] ApplyJobRequest? request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var application = await _workerJobService.ApplyToJobAsync(userId.Value, id, request);
            return Ok(application);
        }
        catch (JobNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DuplicateApplicationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("applications")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<WorkerJobApplicationResponse>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyApplications()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _workerJobService.GetMyApplicationsAsync(userId.Value));
    }

    [HttpGet("assignments")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<WorkerAssignmentResponse>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyAssignments()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerAssignmentService.GetMyAssignmentsAsync(userId.Value));
        }
        catch (ProfileNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("assignments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerAssignmentResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignmentDetails(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerAssignmentService.GetAssignmentDetailsAsync(userId.Value, id));
        }
        catch (JobNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ProfileNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("attendance")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerAttendanceSummaryResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyAttendance()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerAttendanceService.GetMyAttendanceHistoryAsync(userId.Value));
        }
        catch (ProfileNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("assignments/{assignmentId:guid}/attendance")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkerAttendanceSummaryResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignmentAttendance(Guid assignmentId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await _workerAttendanceService.GetAssignmentAttendanceAsync(userId.Value, assignmentId));
        }
        catch (JobNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ProfileNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }
}
