using FarmKart.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerProfileService
{
    Task<WorkerProfileResponse> GetProfileAsync(Guid userId);
    Task<WorkerProfileResponse> UpdateProfileAsync(Guid userId, WorkerProfileUpdateRequest request);
}
