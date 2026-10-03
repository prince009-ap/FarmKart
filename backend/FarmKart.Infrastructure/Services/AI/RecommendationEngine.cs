using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace FarmKart.Infrastructure.Services.AI;

/// <summary>
/// Core generic recommendation engine.
///
/// Pipeline:
///   Input validation
///     → Candidate count safeguard (max 50)
///     → Deterministic score normalization
///     → MinMatchScore filter
///     → Optional AI reasoning enrichment
///     → Confidence calculation
///     → Top-N ranking
///     → Result assembly
///
/// This class has NO domain-specific knowledge.  It does NOT query the database,
/// does NOT know about Workers, Machinery, or Crops, and does NOT mutate records.
/// </summary>
public sealed class RecommendationEngine : IRecommendationEngine
{
    // ── Safety limits ────────────────────────────────────────────────────────
    private const int MaxCandidatesProcessed = 50;
    private const int MaxResultsAllowed = 20;
    private const int MinResultsAllowed = 1;

    private readonly IAiRecommendationProvider _aiProvider;
    private readonly ILogger<RecommendationEngine> _logger;

    public RecommendationEngine(
        IAiRecommendationProvider aiProvider,
        ILogger<RecommendationEngine> logger)
    {
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<RecommendationResponse> RecommendAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startTime = DateTime.UtcNow;
        _logger.LogInformation(
            "Recommendation request started. Type={RecommendationType} UserId={UserId} Candidates={CandidateCount} MaxResults={MaxResults}",
            request.RecommendationType,
            request.RequestingUserId,
            request.Candidates?.Count ?? 0,
            request.MaxResults);

        // ── 1. Input validation ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.RecommendationType))
            throw new ArgumentException("RecommendationType is required.", nameof(request));

        if (request.Candidates is null || request.Candidates.Count == 0)
        {
            _logger.LogInformation(
                "No candidates provided for recommendation. Type={RecommendationType}",
                request.RecommendationType);
            return EmptyResponse(request.RecommendationType, "No candidates available for recommendation.");
        }

        // Clamp MaxResults
        var maxResults = Math.Clamp(request.MaxResults, MinResultsAllowed, MaxResultsAllowed);

        // Clamp MinMatchScore to valid range
        var minScore = Math.Clamp(request.MinMatchScore, 0.0, 100.0);

        // ── 2. Candidate count safeguard ─────────────────────────────────────
        var candidates = request.Candidates;
        if (candidates.Count > MaxCandidatesProcessed)
        {
            _logger.LogWarning(
                "Candidate count {CandidateCount} exceeds maximum {Max}. Trimming to {Max}. Type={RecommendationType}",
                candidates.Count, MaxCandidatesProcessed, request.RecommendationType);
            candidates = candidates.Take(MaxCandidatesProcessed).ToList();
        }

        // ── 3. Deterministic score computation from factors ──────────────────
        var scoredCandidates = candidates
            .Select(c => (Candidate: c, RawScore: ComputeWeightedScore(c.Factors)))
            .ToList();

        // ── 4. Normalize scores to [0–100] ───────────────────────────────────
        double maxRaw = scoredCandidates.Count > 0
            ? scoredCandidates.Max(x => x.RawScore)
            : 1.0;
        if (maxRaw <= 0) maxRaw = 1.0; // prevent division by zero

        var normalized = scoredCandidates
            .Select(x => (
                Candidate: x.Candidate,
                NormalizedScore: Math.Round(Math.Min((x.RawScore / maxRaw) * 100.0, 100.0), 2)
            ))
            .ToList();

        // ── 5. Apply minimum match score filter ──────────────────────────────
        var filtered = normalized
            .Where(x => x.NormalizedScore >= minScore)
            .OrderByDescending(x => x.NormalizedScore)
            .ToList();

        if (filtered.Count == 0)
        {
            _logger.LogInformation(
                "All candidates filtered out by MinMatchScore={MinScore}. Type={RecommendationType}",
                minScore, request.RecommendationType);
            return EmptyResponse(request.RecommendationType, $"No candidates met the minimum score threshold of {minScore}.");
        }

        // ── 6. Optional AI reasoning enrichment ──────────────────────────────
        bool aiEnriched = false;
        string? fallbackReason = null;
        Dictionary<string, AiRecommendationItemDto> aiResults = new(StringComparer.OrdinalIgnoreCase);

        if (!request.ForceDeterministicOnly)
        {
            var summaries = filtered
                .Take(maxResults * 2) // only top candidates go to AI to stay within prompt limits
                .Select(x => new CandidateSummaryDto(
                    EntityId: x.Candidate.EntityId,
                    NormalizedScore: x.NormalizedScore,
                    Attributes: x.Candidate.Metadata ?? new Dictionary<string, string>(),
                    FactorReasons: x.Candidate.Factors
                        .OrderByDescending(f => f.WeightedScore)
                        .Select(f => f.Reason)
                        .ToList()
                ))
                .ToList();

            try
            {
                _logger.LogInformation(
                    "Requesting AI reasoning enrichment. Type={RecommendationType} Provider=AiRecommendationProvider Summaries={Count}",
                    request.RecommendationType, summaries.Count);

                var aiItems = await _aiProvider.GetRecommendationReasonsAsync(
                    request.RecommendationType,
                    request.ContextSummary,
                    summaries,
                    request.Language,
                    maxResults,
                    cancellationToken);

                // Index by EntityId, but ONLY allow IDs that exist in our candidate set
                var validIds = filtered.Select(x => x.Candidate.EntityId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var item in aiItems)
                {
                    if (validIds.Contains(item.EntityId))
                    {
                        aiResults[item.EntityId] = item;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "AI provider returned unknown EntityId={EntityId}. Discarded. Type={RecommendationType}",
                            item.EntityId, request.RecommendationType);
                    }
                }

                aiEnriched = aiResults.Count > 0;
                _logger.LogInformation(
                    "AI enrichment completed. Enriched={Count} Type={RecommendationType}",
                    aiResults.Count, request.RecommendationType);
            }
            catch (OperationCanceledException)
            {
                fallbackReason = "AI enrichment was cancelled. Deterministic scores used.";
                _logger.LogWarning(
                    "AI enrichment cancelled. Using deterministic fallback. Type={RecommendationType}",
                    request.RecommendationType);
            }
            catch (TimeoutException ex)
            {
                fallbackReason = "AI provider timed out. Deterministic scores used.";
                _logger.LogWarning(ex,
                    "AI provider timeout during recommendation enrichment. Type={RecommendationType}",
                    request.RecommendationType);
            }
            catch (Exception ex)
            {
                fallbackReason = "AI enrichment unavailable. Deterministic scores used.";
                _logger.LogError(ex,
                    "AI provider failure during recommendation enrichment. Type={RecommendationType}",
                    request.RecommendationType);
            }
        }
        else
        {
            fallbackReason = "AI enrichment skipped (deterministic-only mode).";
        }

        // ── 7. Assemble final results ─────────────────────────────────────────
        var results = new List<RecommendationResult>();

        foreach (var (candidate, normalizedScore) in filtered.Take(maxResults))
        {
            aiResults.TryGetValue(candidate.EntityId, out var aiItem);

            // Collect reasons: deterministic factor reasons first, then AI reasons
            var reasons = candidate.Factors
                .OrderByDescending(f => f.WeightedScore)
                .Select(f => f.Reason)
                .ToList();

            if (aiItem?.Reasons is { Count: > 0 })
            {
                foreach (var r in aiItem.Reasons)
                {
                    if (!string.IsNullOrWhiteSpace(r))
                        reasons.Add(r);
                }
            }

            var warnings = new List<string>();
            if (aiItem?.Warnings is { Count: > 0 })
            {
                warnings.AddRange(aiItem.Warnings.Where(w => !string.IsNullOrWhiteSpace(w)));
            }

            // Confidence: if AI provided one, blend with normalized score; otherwise estimate from normalized score
            double confidence;
            if (aiItem is not null)
            {
                double aiConfidence = Math.Clamp(aiItem.ConfidenceScore, 0.0, 1.0);
                // Weighted blend: 60% AI, 40% deterministic signal
                confidence = Math.Round((aiConfidence * 0.6) + ((normalizedScore / 100.0) * 0.4), 3);
            }
            else
            {
                // Deterministic confidence estimate: simply scale the normalized score
                confidence = Math.Round(normalizedScore / 100.0, 3);
            }

            results.Add(new RecommendationResult(
                EntityId: candidate.EntityId,
                EntityType: candidate.EntityType,
                MatchScore: normalizedScore,
                ConfidenceScore: confidence,
                Reasons: reasons.Distinct().ToList(),
                Warnings: warnings,
                Metadata: candidate.Metadata
            ));
        }

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation(
            "Recommendation generation completed. Type={RecommendationType} Results={Count} AiEnriched={AiEnriched} Duration={DurationMs}ms",
            request.RecommendationType, results.Count, aiEnriched, elapsed);

        return new RecommendationResponse(
            RecommendationType: request.RecommendationType,
            Results: results,
            AiEnriched: aiEnriched,
            FallbackReason: fallbackReason);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Sums all weighted factor contributions.
    /// Score = Σ(RawScore_i × Weight_i).
    /// Overflow is prevented by clamping each factor's RawScore to [0, 100].
    /// </summary>
    private static double ComputeWeightedScore(IReadOnlyList<RecommendationFactor> factors)
    {
        if (factors is null || factors.Count == 0) return 0.0;

        double total = 0;
        foreach (var f in factors)
        {
            double clampedRaw = Math.Clamp(f.RawScore, 0.0, 100.0);
            double clampedWeight = Math.Max(f.Weight, 0.0);
            total += clampedRaw * clampedWeight;
        }
        return total;
    }

    private static RecommendationResponse EmptyResponse(string type, string reason) =>
        new(
            RecommendationType: type,
            Results: Array.Empty<RecommendationResult>(),
            AiEnriched: false,
            FallbackReason: reason);
}
