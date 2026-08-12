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

    [HttpPost("register/worker")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(WorkerRegistrationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterWorker([FromBody] WorkerRegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterWorkerAsync(request);
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

    [HttpPost("register/customer")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CustomerRegistrationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterCustomer([FromBody] CustomerRegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterCustomerAsync(request);
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

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}
