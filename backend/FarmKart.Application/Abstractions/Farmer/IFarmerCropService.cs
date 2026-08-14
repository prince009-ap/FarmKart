using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerCropService
{
    Task<IReadOnlyList<CropResponse>> GetFarmerCropsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CropResponse?> GetCropByIdAsync(Guid userId, Guid cropId, CancellationToken cancellationToken = default);
    Task<CropResponse> CreateCropAsync(Guid userId, CreateCropRequest request, CancellationToken cancellationToken = default);
    Task<CropResponse> UpdateCropAsync(Guid userId, Guid cropId, UpdateCropRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteCropAsync(Guid userId, Guid cropId, CancellationToken cancellationToken = default);

    Task<CropImageResponse> UploadCropImageAsync(Guid userId, Guid cropId, Stream fileStream, string fileName, string contentType, long fileLength, bool isPrimary = false, CancellationToken cancellationToken = default);
    Task<bool> DeleteCropImageAsync(Guid userId, Guid cropId, Guid imageId, CancellationToken cancellationToken = default);
    Task<CropResponse> SetPrimaryCropImageAsync(Guid userId, Guid cropId, Guid imageId, CancellationToken cancellationToken = default);
}
