using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiController> _logger;

    public AiController(IAiService aiService, ILogger<AiController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AiChatResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _aiService.ChatAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid AI chat request: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("AI chat timeout: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "AI service invalid operation exception: {Message}", ex.Message);
            if (ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("busy", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
            }
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled AI service error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "AI service is temporarily unavailable. Please try again." });
        }
    }
}
