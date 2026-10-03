using System;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Farmer;

/// <summary>
/// Service abstraction for AI Worker Recommendation (AI-02).
/// Ranks available, eligible workers for a Farmer's job using the generic AI-01 Recommendation Engine.
/// </summary>
public interface IWorkerRecommendationService
{
    /// <summary>
    /// Evaluates eligible workers against a job's requirements and returns ranked worker recommendations.
    /// All scoring is authoritative and computed server-side.
    /// </summary>
    /// <param name="farmerUserId">Authenticated Farmer's User ID (derived from JWT claims).</param>
    /// <param name="jobId">Target job ID.</param>
    /// <param name="topN">Maximum recommendations to return (1 to 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WorkerRecommendationResponse> GetRecommendedWorkersAsync(
        Guid farmerUserId,
        Guid jobId,
        int topN = 5,
        CancellationToken cancellationToken = default);
}
