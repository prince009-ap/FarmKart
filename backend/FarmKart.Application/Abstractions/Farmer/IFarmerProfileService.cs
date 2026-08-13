using FarmKart.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerProfileService
{
    /// <summary>
    /// Retrieves the FarmerProfile for the authenticated farmer identified by <paramref name="userId"/>.
    /// Throws <see cref="Application.Exceptions.ProfileNotFoundException"/> if no profile is found.
    /// </summary>
    Task<FarmerProfileResponse> GetProfileAsync(Guid userId);

    /// <summary>
    /// Updates the FarmerProfile for the authenticated farmer identified by <paramref name="userId"/>.
    /// Returns the updated profile.
    /// Throws <see cref="Application.Exceptions.ProfileNotFoundException"/> if no profile is found.
    /// </summary>
    Task<FarmerProfileResponse> UpdateProfileAsync(Guid userId, FarmerProfileUpdateRequest request);
}
