using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.AI;

/// <summary>
/// Core recommendation engine interface.
///
/// Future modules call this with a <see cref="RecommendationRequest"/> that
/// already contains pre-filtered, pre-scored <see cref="RecommendationCandidate"/>
/// instances.  The engine handles:
///   1. Candidate validation and clamping (max candidates safeguard).
///   2. Optional AI reasoning enrichment via <see cref="IAiRecommendationProvider"/>.
///   3. Score normalization and confidence calculation.
///   4. Top-N ranking and result assembly.
///
/// The engine does NOT query the database, does NOT perform business-specific
/// scoring, and does NOT mutate any records.
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>
    /// Runs the recommendation pipeline and returns ranked results.
    /// All scores are computed server-side; client-supplied scores are ignored.
    /// </summary>
    Task<RecommendationResponse> RecommendAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken = default);
}
