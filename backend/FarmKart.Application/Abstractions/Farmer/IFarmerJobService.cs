using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerJobService
{
    Task<IReadOnlyList<FarmerJobResponse>> GetJobsAsync(Guid userId);
    Task<FarmerJobResponse> GetJobAsync(Guid userId, Guid jobId);
    Task<FarmerJobResponse> CreateJobAsync(Guid userId, CreateFarmerJobRequest request);
    Task<FarmerJobResponse> UpdateJobAsync(Guid userId, Guid jobId, UpdateFarmerJobRequest request);
    Task CancelJobAsync(Guid userId, Guid jobId);
}
