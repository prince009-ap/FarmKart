using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/ai/conversation")]
[Authorize]
public class AiConversationController : ControllerBase
{
    private readonly IAiConversationEngine _conversationEngine;

    public AiConversationController(IAiConversationEngine conversationEngine)
    {
        _conversationEngine = conversationEngine;
    }

    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AiConversationStateResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Start([FromBody] StartAiConversationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _conversationEngine.StartConversationAsync(userId.Value, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("message")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AiConversationStateResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage([FromBody] SendAiConversationMessageRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _conversationEngine.ProcessMessageAsync(userId.Value, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Cancel([FromBody] CancelAiConversationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        await _conversationEngine.CancelConversationAsync(userId.Value, request, cancellationToken);
        return Ok(new { message = "Conversation cancelled successfully. Your changes have not been saved." });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
