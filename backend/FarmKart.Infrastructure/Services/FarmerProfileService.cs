using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Enums;
using FarmKart.Domain.ValueObjects;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public class FarmerProfileService : IFarmerProfileService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public FarmerProfileService(FarmKartDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<FarmerProfileResponse> GetProfileAsync(Guid userId)
    {
        var profile = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            throw new ProfileNotFoundException();
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        var email = user?.Email ?? string.Empty;

        return new FarmerProfileResponse(
            UserId: profile.UserId,
            FullName: profile.FullName,
            Email: email,
            Phone: profile.Phone,
            Address: profile.AddressInfo.AddressLine,
            FarmName: profile.FarmName,
            FarmSize: profile.FarmSize,
            FarmSizeUnit: profile.FarmSizeUnit,
            FarmLocation: profile.FarmLocation
        );
    }

    public async Task<FarmerProfileResponse> UpdateProfileAsync(Guid userId, FarmerProfileUpdateRequest request)
    {
        // Validate FarmSizeUnit if provided
        if (request.FarmSizeUnit.HasValue && !Enum.IsDefined(typeof(FarmSizeUnit), request.FarmSizeUnit.Value))
        {
            throw new ArgumentException("FarmSizeUnit must be a supported value.");
        }

        var profile = await _dbContext.FarmerProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            throw new ProfileNotFoundException();
        }

        profile.FullName = request.FullName.Trim();
        profile.Phone = request.Phone.Trim();
        profile.FarmName = string.IsNullOrWhiteSpace(request.FarmName) ? null : request.FarmName.Trim();
        profile.FarmSize = request.FarmSize;
        profile.FarmSizeUnit = request.FarmSizeUnit;
        profile.FarmLocation = string.IsNullOrWhiteSpace(request.FarmLocation) ? null : request.FarmLocation.Trim();
        profile.AddressInfo = new AddressInfo
        {
            AddressLine = request.Address.Trim()
        };

        await _dbContext.SaveChangesAsync();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        var email = user?.Email ?? string.Empty;

        return new FarmerProfileResponse(
            UserId: profile.UserId,
            FullName: profile.FullName,
            Email: email,
            Phone: profile.Phone,
            Address: profile.AddressInfo.AddressLine,
            FarmName: profile.FarmName,
            FarmSize: profile.FarmSize,
            FarmSizeUnit: profile.FarmSizeUnit,
            FarmLocation: profile.FarmLocation
        );
    }
}
