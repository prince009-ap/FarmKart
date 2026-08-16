using FarmKart.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerProfileService
{
    Task<FarmerProfileResponse> GetProfileAsync(Guid userId);
    Task<FarmerProfileResponse> UpdateProfileAsync(Guid userId, FarmerProfileUpdateRequest request);
    Task<FarmerPublicProfileResponse?> GetPublicFarmerProfileAsync(string farmerIdOrUserId, CancellationToken cancellationToken = default);
}
