using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace FarmKart.Infrastructure.Services.AI;

/// <summary>
/// AI reasoning enrichment provider for the Recommendation Engine.
///
/// Delegates to the existing <see cref="IAiProvider"/> (Gemini or OpenAI) so
/// there is only ONE AI HTTP client in the system.  This class adds:
///   - Structured JSON prompt with strict output schema.
///   - Response parsing and validation.
///   - Score clamping (rejects AI-hallucinated scores outside bounds).
///   - EntityId whitelist enforcement.
///   - Safe empty-list fallback on any malformed response.
///
/// API keys, provider URLs and retry logic are already handled by
/// <see cref="GeminiProvider"/> / <see cref="OpenAiProvider"/>.
/// This class reuses them via <see cref="IAiProvider"/>.
/// </summary>
public sealed class AiRecommendationProvider : IAiRecommendationProvider
{
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<AiRecommendationProvider> _logger;

    // Maximum number of candidates whose summaries are included in the prompt
    private const int MaxCandidatesInPrompt = 15;
    // Maximum characters sent in the context summary
    private const int MaxContextSummaryLength = 800;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AiRecommendationProvider(IAiProvider aiProvider, ILogger<AiRecommendationProvider> logger)
    {
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiRecommendationItemDto>> GetRecommendationReasonsAsync(
        string recommendationType,
        string contextSummary,
        IReadOnlyList<CandidateSummaryDto> candidateSummaries,
        string language,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (candidateSummaries is null || candidateSummaries.Count == 0)
            return Array.Empty<AiRecommendationItemDto>();

        // Trim prompt inputs to safe limits
        var safeContext = (contextSummary ?? string.Empty).Length > MaxContextSummaryLength
            ? contextSummary!.Substring(0, MaxContextSummaryLength)
            : contextSummary ?? string.Empty;

        var candidatesForPrompt = candidateSummaries.Take(MaxCandidatesInPrompt).ToList();

        // Build compact candidate list — deliberately excludes sensitive fields
        var candidateLines = new StringBuilder();
        foreach (var c in candidatesForPrompt)
        {
            var attrs = string.Join(", ", c.Attributes
                .Take(6) // limit attributes per candidate to keep prompt size bounded
                .Select(kv => $"{kv.Key}: {kv.Value}"));

            var factorSummary = string.Join("; ", c.FactorReasons.Take(3));
            candidateLines.AppendLine(
                $"- ID: {c.EntityId} | Score: {c.NormalizedScore:F1} | Attributes: [{attrs}] | Reasons: [{factorSummary}]");
        }

        var systemPrompt = """
            You are a structured data analyst for FarmKart, an agricultural marketplace platform.
            Your task is to reason about pre-scored candidates and return structured JSON.
            Rules:
            1. Return ONLY a valid JSON object with a "recommendations" array.
            2. Include only IDs from the provided candidate list — never invent new IDs.
            3. Clamp matchScore to an integer between 0 and 100.
            4. Clamp confidenceScore to a decimal between 0.0 and 1.0.
            5. reasons array: 1–3 short, helpful, non-technical strings.
            6. warnings array: empty [] if no concerns; else 1–2 short warning strings.
            7. Do not include personal information, API keys, or internal system details.
            8. Respond only with the JSON object, nothing else.
            """;

        var userMessage = $$"""
            Recommendation type: {{recommendationType}}
            Language for reasons/warnings: {{language}}
            Context: {{safeContext}}
            Return top {{maxResults}} recommendations from these candidates:
            {{candidateLines}}

            Required output format (strict JSON, no markdown):
            {
              "recommendations": [
                {
                  "entityId": "...",
                  "matchScore": 85,
                  "confidenceScore": 0.82,
                  "reasons": ["reason one", "reason two"],
                  "warnings": []
                }
              ]
            }
            """;

        string? rawResponse = null;
        try
        {
            rawResponse = await _aiProvider.GenerateResponseAsync(
                systemPrompt,
                conversationHistory: null,
                userMessage,
                language,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AI provider call cancelled during recommendation reasoning.");
            throw; // let the engine handle cancellation
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "AI provider timed out during recommendation reasoning.");
            throw; // let the engine handle timeout and apply fallback
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI provider failure during recommendation reasoning. Returning empty list.");
            return Array.Empty<AiRecommendationItemDto>();
        }

        return ParseAndValidateResponse(rawResponse, candidateSummaries, maxResults);
    }

    // ── Private parsing ────────────────────────────────────────────────────────

    private IReadOnlyList<AiRecommendationItemDto> ParseAndValidateResponse(
        string? rawResponse,
        IReadOnlyList<CandidateSummaryDto> validCandidates,
        int maxResults)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            _logger.LogWarning("AI recommendation provider returned empty response. Using deterministic fallback.");
            return Array.Empty<AiRecommendationItemDto>();
        }

        // Extract JSON object — AI sometimes wraps it in markdown
        var jsonMatch = Regex.Match(rawResponse, @"\{[\s\S]*\}", RegexOptions.Singleline);
        var jsonText = jsonMatch.Success ? jsonMatch.Value : rawResponse.Trim();

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            if (!root.TryGetProperty("recommendations", out var recsElement) ||
                recsElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("AI response missing 'recommendations' array. Raw={Raw}", SanitizeForLog(rawResponse));
                return Array.Empty<AiRecommendationItemDto>();
            }

            var validIds = validCandidates
                .Select(c => c.EntityId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var results = new List<AiRecommendationItemDto>();

            foreach (var elem in recsElement.EnumerateArray())
            {
                if (!elem.TryGetProperty("entityId", out var idProp)) continue;
                var entityId = idProp.GetString();
                if (string.IsNullOrWhiteSpace(entityId) || !validIds.Contains(entityId)) continue;

                // matchScore: clamp to [0, 100]
                int matchScore = 50;
                if (elem.TryGetProperty("matchScore", out var msProp))
                    matchScore = Math.Clamp(msProp.GetInt32(), 0, 100);

                // confidenceScore: clamp to [0.0, 1.0]
                double confidence = 0.5;
                if (elem.TryGetProperty("confidenceScore", out var csProp))
                    confidence = Math.Clamp(csProp.GetDouble(), 0.0, 1.0);

                // reasons
                var reasons = ExtractStringArray(elem, "reasons", maxItems: 3);

                // warnings
                var warnings = ExtractStringArray(elem, "warnings", maxItems: 2);

                results.Add(new AiRecommendationItemDto(
                    EntityId: entityId,
                    MatchScore: matchScore,
                    ConfidenceScore: confidence,
                    Reasons: reasons,
                    Warnings: warnings));

                if (results.Count >= maxResults) break;
            }

            return results;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed JSON from AI recommendation provider. Using deterministic fallback.");
            return Array.Empty<AiRecommendationItemDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing AI recommendation response. Using deterministic fallback.");
            return Array.Empty<AiRecommendationItemDto>();
        }
    }

    private static IReadOnlyList<string> ExtractStringArray(JsonElement element, string propertyName, int maxItems)
    {
        var list = new List<string>();
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in prop.EnumerateArray())
        {
            var s = item.GetString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                list.Add(s.Trim());
                if (list.Count >= maxItems) break;
            }
        }
        return list;
    }

    /// <summary>Truncates raw AI response for safe logging — never log full prompts.</summary>
    private static string SanitizeForLog(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? "(empty)" : raw[..Math.Min(raw.Length, 200)];
}
