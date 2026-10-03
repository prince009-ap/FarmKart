using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

// ─── Scoring Building Blocks ────────────────────────────────────────────────

/// <summary>
/// Represents a single weighted scoring factor produced by a deterministic scorer.
/// Future modules (Worker, Machinery, Crop) define their own factor types.
/// </summary>
public record RecommendationFactor(
    string Name,
    double RawScore,
    double Weight,
    string Reason
)
{
    /// <summary>Weighted contribution of this factor (RawScore × Weight).</summary>
    public double WeightedScore => RawScore * Weight;
}

// ─── Candidate ──────────────────────────────────────────────────────────────

/// <summary>
/// A lightweight candidate passed into the recommendation engine for scoring.
/// Entity-specific data belongs in the module's own scorer; this carries only
/// what the generic engine needs to orchestrate the pipeline.
/// </summary>
public record RecommendationCandidate(
    /// <summary>Entity primary key, role-appropriate (Worker ID, Machinery ID, …).</summary>
    string EntityId,

    /// <summary>Human-readable entity type tag, e.g. "Worker", "Machinery", "Crop".</summary>
    string EntityType,

    /// <summary>Pre-computed deterministic score factors injected by the module scorer.</summary>
    IReadOnlyList<RecommendationFactor> Factors,

    /// <summary>
    /// Non-sensitive metadata the module wants to surface in the result
    /// (e.g. "name", "location", "category"). Must NOT contain PII or secrets.
    /// </summary>
    IReadOnlyDictionary<string, string>? Metadata = null
);

// ─── Request ─────────────────────────────────────────────────────────────────

/// <summary>
/// Generic recommendation request.  Each future module builds one of these and
/// hands it to <see cref="IRecommendationEngine"/> — it never calls the engine
/// with entity-specific parameters.
/// </summary>
public record RecommendationRequest(
    /// <summary>Caller user ID (from JWT claims). Never trust a client-supplied score.</summary>
    System.Guid RequestingUserId,

    /// <summary>Logical recommendation type, e.g. "Worker", "Machinery", "Crop".</summary>
    string RecommendationType,

    /// <summary>Pre-filtered, pre-scored candidate list supplied by the module scorer.</summary>
    IReadOnlyList<RecommendationCandidate> Candidates,

    /// <summary>Free-text context description sent to the AI for reasoning enrichment.</summary>
    string ContextSummary,

    /// <summary>Language code for AI reasoning text, one of "en", "hi", "gu".</summary>
    string Language = "en",

    /// <summary>Maximum recommendations to return. Clamped to [1, MaxAllowedResults].</summary>
    int MaxResults = 5,

    /// <summary>
    /// Minimum normalized match score [0–100] for a candidate to be included.
    /// Candidates below this threshold are dropped before AI enrichment.
    /// </summary>
    double MinMatchScore = 0.0,

    /// <summary>
    /// When true the engine skips AI reasoning enrichment and returns purely
    /// deterministic scores.  Useful for fast paths or when AI is unavailable.
    /// </summary>
    bool ForceDeterministicOnly = false
);

// ─── Result ───────────────────────────────────────────────────────────────────

/// <summary>
/// A single recommendation result. All scores are generated server-side;
/// never accept MatchScore or ConfidenceScore from a client.
/// </summary>
public record RecommendationResult(
    /// <summary>Entity primary key.</summary>
    string EntityId,

    /// <summary>Human-readable entity type tag.</summary>
    string EntityType,

    /// <summary>
    /// Normalized match score [0–100].  Higher is better.
    /// Generated from weighted factor sum — never from raw AI output alone.
    /// </summary>
    double MatchScore,

    /// <summary>
    /// AI-assisted confidence [0.0–1.0].
    /// Falls back to a deterministic estimate when the AI provider is unavailable.
    /// </summary>
    double ConfidenceScore,

    /// <summary>Human-readable explanations produced by deterministic scoring + AI reasoning.</summary>
    IReadOnlyList<string> Reasons,

    /// <summary>Optional warnings about missing data or low confidence.</summary>
    IReadOnlyList<string> Warnings,

    /// <summary>
    /// Pass-through module-level metadata (non-sensitive).
    /// Sourced from <see cref="RecommendationCandidate.Metadata"/>.
    /// </summary>
    IReadOnlyDictionary<string, string>? Metadata = null
);

// ─── Engine Response ─────────────────────────────────────────────────────────

/// <summary>
/// Typed response wrapper returned by <see cref="IRecommendationEngine"/>.
/// </summary>
public record RecommendationResponse(
    string RecommendationType,
    IReadOnlyList<RecommendationResult> Results,

    /// <summary>True when AI enrichment was used for at least one candidate.</summary>
    bool AiEnriched,

    /// <summary>Non-empty when AI enrichment was skipped or partially failed.</summary>
    string? FallbackReason = null
);

// ─── AI Provider Output ───────────────────────────────────────────────────────

/// <summary>
/// DTO representing the structured JSON item returned by AI provider reasoning.
/// </summary>
public record AiRecommendationItemDto(
    string EntityId,
    int MatchScore,
    double ConfidenceScore,
    IReadOnlyList<string>? Reasons,
    IReadOnlyList<string>? Warnings
);
