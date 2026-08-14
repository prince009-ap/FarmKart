using FarmKart.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerProfileService
{
    Task<WorkerProfileResponse> GetProfileAsync(Guid userId);
    Task<WorkerProfileResponse> UpdateProfileAsync(Guid userId, WorkerProfileUpdateRequest request);
    Task<WorkerPreferencesResponse> GetPreferencesAsync(Guid userId);
    Task<WorkerPreferencesResponse> UpdatePreferencesAsync(Guid userId, WorkerPreferencesUpdateRequest request);
}
