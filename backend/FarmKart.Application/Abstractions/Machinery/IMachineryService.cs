using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Machinery;

public interface IMachineryService
{
    /// <summary>Browse public machinery listings with filters and pagination.</summary>
    Task<PagedMachineryResponse> GetMachineryAsync(
        MachineryFilterRequest filter,
        string? currentUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Get a single machinery by ID.</summary>
    Task<MachineryResponse?> GetMachineryByIdAsync(
        Guid machineryId,
        string? currentUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Get all machinery owned by the authenticated user.</summary>
    Task<IReadOnlyList<MachineryResponse>> GetMyMachineryAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Create a new machinery listing.</summary>
    Task<MachineryResponse> CreateMachineryAsync(
        string ownerUserId,
        CreateMachineryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Update an owned machinery listing.</summary>
    Task<MachineryResponse> UpdateMachineryAsync(
        string ownerUserId,
        Guid machineryId,
        UpdateMachineryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-delete an owned machinery listing (sets IsActive=false).</summary>
    Task<bool> DeleteMachineryAsync(
        string ownerUserId,
        Guid machineryId,
        CancellationToken cancellationToken = default);

    /// <summary>Get booked date ranges to show availability calendar.</summary>
    Task<MachineryAvailabilityResponse> GetAvailabilityAsync(
        Guid machineryId,
        CancellationToken cancellationToken = default);

    /// <summary>Upload an image for owned machinery.</summary>
    Task<MachineryImageResponse> UploadMachineryImageAsync(
        string ownerUserId,
        Guid machineryId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        bool isPrimary = false,
        CancellationToken cancellationToken = default);

    /// <summary>Delete an image from owned machinery.</summary>
    Task<bool> DeleteMachineryImageAsync(
        string ownerUserId,
        Guid machineryId,
        Guid imageId,
        CancellationToken cancellationToken = default);

    /// <summary>Set a machinery image as primary.</summary>
    Task<MachineryResponse> SetPrimaryMachineryImageAsync(
        string ownerUserId,
        Guid machineryId,
        Guid imageId,
        CancellationToken cancellationToken = default);
}
