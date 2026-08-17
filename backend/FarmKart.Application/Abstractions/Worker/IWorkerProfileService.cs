using FarmKart.Application.DTOs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerProfileService
{
    Task<WorkerProfileResponse> GetProfileAsync(Guid userId);
    Task<WorkerProfileResponse> UpdateProfileAsync(Guid userId, WorkerProfileUpdateRequest request);
    Task<WorkerPreferencesResponse> GetPreferencesAsync(Guid userId);
    Task<WorkerPreferencesResponse> UpdatePreferencesAsync(Guid userId, WorkerPreferencesUpdateRequest request);
    Task<WorkerProfileResponse> UploadProfileImageAsync(Guid userId, Stream stream, string fileName, string contentType, long fileLength, CancellationToken cancellationToken = default);
    Task<WorkerProfileResponse> RemoveProfileImageAsync(Guid userId, CancellationToken cancellationToken = default);
}
