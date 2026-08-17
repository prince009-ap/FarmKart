using FarmKart.Application.DTOs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerProfileService
{
    Task<FarmerProfileResponse> GetProfileAsync(Guid userId);
    Task<FarmerProfileResponse> UpdateProfileAsync(Guid userId, FarmerProfileUpdateRequest request);
    Task<FarmerPublicProfileResponse?> GetPublicFarmerProfileAsync(string farmerIdOrUserId, CancellationToken cancellationToken = default);
    Task<FarmerProfileResponse> UploadProfileImageAsync(Guid userId, Stream stream, string fileName, string contentType, long fileLength, CancellationToken cancellationToken = default);
    Task<FarmerProfileResponse> RemoveProfileImageAsync(Guid userId, CancellationToken cancellationToken = default);
}
