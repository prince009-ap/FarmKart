using FarmKart.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerReviewService
{
    Task<WorkerReviewResponse> RateWorkerAsync(Guid farmerUserId, Guid assignmentId, CreateWorkerReviewRequest request);
    Task<WorkerReviewResponse?> GetAssignmentReviewAsync(Guid farmerUserId, Guid assignmentId);
    Task<WorkerRatingSummaryResponse> GetWorkerRatingSummaryAsync(Guid workerUserId);
    Task<WorkerRatingSummaryResponse> GetWorkerRatingSummaryByProfileIdAsync(Guid workerProfileId);
}
