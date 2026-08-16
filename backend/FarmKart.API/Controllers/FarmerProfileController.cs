using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/farmers")]
[Authorize]
public sealed class FarmerProfileController : ControllerBase
{
    private readonly IFarmerProfileService _farmerProfileService;

    public FarmerProfileController(IFarmerProfileService farmerProfileService)
    {
        _farmerProfileService = farmerProfileService;
    }

    [HttpGet("{farmerId}/profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FarmerPublicProfileResponse))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(object))]
    public async Task<IActionResult> GetPublicProfile(string farmerId, CancellationToken cancellationToken)
    {
        var profile = await _farmerProfileService.GetPublicFarmerProfileAsync(farmerId, cancellationToken);
        if (profile == null)
        {
            return NotFound(new { message = $"Farmer profile not found for '{farmerId}'." });
        }

        return Ok(profile);
    }
}
