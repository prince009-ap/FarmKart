using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register/farmer")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FarmerRegistrationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterFarmer([FromBody] FarmerRegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterFarmerAsync(request);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (RegistrationFailedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
