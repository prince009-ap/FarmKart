using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.AI;

/// <summary>
/// Abstraction for AI-assisted recommendation reasoning.
///
/// This deliberately separates "AI for recommendations" from the general
/// <see cref="IAiProvider"/> (which is used for chat and conversation flows).
/// Both implementations share the same underlying provider (Gemini / OpenAI),
/// but this interface enforces:
///   - Structured JSON output requirements.
///   - Candidate ID whitelist so the AI cannot hallucinate new entity IDs.
///   - Automatic validation and safe fallback.
///
/// Implementations must NOT expose API keys, raw provider URLs, or internal
/// prompts in any log or exception message sent to the caller.
/// </summary>
public interface IAiRecommendationProvider
{
    /// <summary>
    /// Asks the AI provider to reason about a set of pre-scored candidates and
    /// return confidence scores plus human-readable explanations.
    ///
    /// The provider MUST:
    ///   - Return only entity IDs that exist in <paramref name="candidateSummaries"/>.
    ///   - Clamp MatchScore to [0, 100] and ConfidenceScore to [0.0, 1.0].
    ///   - Return at most <paramref name="maxResults"/> items.
    ///   - Return an empty list on malformed AI response (safe fallback).
    ///   - Not mutate any database records.
    ///
    /// The caller is responsible for:
    ///   - Filtering sensitive information out of <paramref name="candidateSummaries"/> before calling.
    ///   - Never trusting the returned MatchScore as authoritative over the deterministic score.
    /// </summary>
    /// <param name="recommendationType">Logical type, e.g. "Worker", "Machinery".</param>
    /// <param name="contextSummary">Non-sensitive context about what is being recommended.</param>
    /// <param name="candidateSummaries">Safe, non-sensitive summaries of each candidate.</param>
    /// <param name="language">Response language: "en", "hi", or "gu".</param>
    /// <param name="maxResults">Maximum items to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Parsed AI recommendation items. Empty list if AI is unavailable or returns invalid data.
    /// </returns>
    Task<IReadOnlyList<AiRecommendationItemDto>> GetRecommendationReasonsAsync(
        string recommendationType,
        string contextSummary,
        IReadOnlyList<CandidateSummaryDto> candidateSummaries,
        string language,
        int maxResults,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-sensitive candidate summary sent to the AI provider for reasoning.
/// Must NOT contain PII, internal IDs from unrelated tables, or financial data.
/// </summary>
public record CandidateSummaryDto(
    /// <summary>Entity ID (same as <see cref="RecommendationCandidate.EntityId"/>).</summary>
    string EntityId,

    /// <summary>Normalized deterministic score [0–100] from the module scorer.</summary>
    double NormalizedScore,

    /// <summary>Human-readable, non-sensitive attributes, e.g. {"category":"Tractor","city":"Surat"}.</summary>
    IReadOnlyDictionary<string, string> Attributes,

    /// <summary>Factor names and reasons from deterministic scoring.</summary>
    IReadOnlyList<string> FactorReasons
);
