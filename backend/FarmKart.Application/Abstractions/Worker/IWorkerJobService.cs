using FarmKart.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerJobService
{
    Task<IReadOnlyList<WorkerAvailableJobResponse>> GetAvailableJobsAsync(Guid userId);
    Task<WorkerAvailableJobResponse> GetAvailableJobDetailsAsync(Guid userId, Guid jobId);
    Task<WorkerJobApplicationResponse> ApplyToJobAsync(Guid userId, Guid jobId, ApplyJobRequest? request);
    Task<IReadOnlyList<WorkerJobApplicationResponse>> GetMyApplicationsAsync(Guid userId);
}
