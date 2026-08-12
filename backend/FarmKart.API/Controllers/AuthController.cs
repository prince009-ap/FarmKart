using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(IAuthService authService, IOptions<JwtOptions> jwtOptions)
    {
        _authService = authService;
        _jwtOptions = jwtOptions.Value;
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
            var result = await _authService.LoginAsync(request);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = _jwtOptions.CookieSecure,
                SameSite = ParseSameSite(_jwtOptions.CookieSameSite),
                Path = "/",
                Expires = result.ExpiresAt
            };

            HttpContext.Response.Cookies.Append(_jwtOptions.CookieName, result.Token, cookieOptions);

            var response = new LoginResponse(
                UserId: result.UserId,
                Email: result.Email,
                FullName: result.FullName,
                Role: result.Role,
                ExpiresAt: result.ExpiresAt,
                Message: result.Message
            );

            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("test-auth")]
    public IActionResult TestAuth()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        return Ok(new
        {
            userId,
            email,
            role
        });
    }

    private SameSiteMode ParseSameSite(string policy)
    {
        return policy?.ToLower() switch
        {
            "strict" => SameSiteMode.Strict,
            "none" => SameSiteMode.None,
            _ => SameSiteMode.Lax
        };
    }
}
