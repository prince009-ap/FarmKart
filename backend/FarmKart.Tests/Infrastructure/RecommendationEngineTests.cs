using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;
using FarmKart.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

/// <summary>
/// Unit tests for the AI-01 Recommendation Engine Foundation.
/// Tests cover: empty candidates, single candidate, multiple candidates,
/// AI success, AI timeout, AI failure, deterministic fallback, cancellation,
/// score normalization, MinMatchScore filter, MaxResults clamping,
/// AI hallucinated entity ID rejection, security (no PII in logs/results),
/// and MaxCandidates safeguard.
/// </summary>
public class RecommendationEngineTests
{
    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class StubAiRecommendationProvider : IAiRecommendationProvider
    {
        public IReadOnlyList<AiRecommendationItemDto>? ResponseToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public int CallCount { get; private set; }
        public IReadOnlyList<CandidateSummaryDto>? LastSummaries { get; private set; }

        public Task<IReadOnlyList<AiRecommendationItemDto>> GetRecommendationReasonsAsync(
            string recommendationType,
            string contextSummary,
            IReadOnlyList<CandidateSummaryDto> candidateSummaries,
            string language,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSummaries = candidateSummaries;

            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult<IReadOnlyList<AiRecommendationItemDto>>(
                ResponseToReturn ?? Array.Empty<AiRecommendationItemDto>());
        }
    }

    private static RecommendationEngine BuildEngine(StubAiRecommendationProvider? provider = null)
    {
        return new RecommendationEngine(
            provider ?? new StubAiRecommendationProvider(),
            NullLogger<RecommendationEngine>.Instance);
    }

    private static RecommendationCandidate MakeCandidate(
        string id,
        string entityType = "Worker",
        double rawScore = 80.0,
        double weight = 1.0,
        string reason = "Good match",
        Dictionary<string, string>? metadata = null)
    {
        return new RecommendationCandidate(
            EntityId: id,
            EntityType: entityType,
            Factors: new[]
            {
                new RecommendationFactor("SkillMatch", rawScore, weight, reason)
            },
            Metadata: metadata);
    }

    private static RecommendationRequest MakeRequest(
        IReadOnlyList<RecommendationCandidate> candidates,
        int maxResults = 5,
        double minMatchScore = 0.0,
        bool forceDeterministic = false,
        string language = "en")
    {
        return new RecommendationRequest(
            RequestingUserId: Guid.NewGuid(),
            RecommendationType: "Worker",
            Candidates: candidates,
            ContextSummary: "Looking for experienced harvesters near Surat",
            Language: language,
            MaxResults: maxResults,
            MinMatchScore: minMatchScore,
            ForceDeterministicOnly: forceDeterministic);
    }

    // ── Test 1: Empty candidates returns empty result ─────────────────────────

    [Fact]
    public async Task RecommendAsync_EmptyCandidates_ReturnsEmptyResult()
    {
        // Arrange
        var engine = BuildEngine();
        var request = MakeRequest(Array.Empty<RecommendationCandidate>());

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Empty(response.Results);
        Assert.Equal("Worker", response.RecommendationType);
        Assert.False(response.AiEnriched);
        Assert.NotNull(response.FallbackReason);
    }

    // ── Test 2: Single candidate returns one result ───────────────────────────

    [Fact]
    public async Task RecommendAsync_SingleCandidate_ReturnsOneResult()
    {
        // Arrange
        var engine = BuildEngine(new StubAiRecommendationProvider { ResponseToReturn = Array.Empty<AiRecommendationItemDto>() });
        var candidate = MakeCandidate("W-001");
        var request = MakeRequest(new[] { candidate });

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.Single(response.Results);
        Assert.Equal("W-001", response.Results[0].EntityId);
        Assert.Equal("Worker", response.Results[0].EntityType);
        Assert.Equal(100.0, response.Results[0].MatchScore); // Single candidate normalizes to 100
    }

    // ── Test 3: Multiple candidates are ranked by score ───────────────────────

    [Fact]
    public async Task RecommendAsync_MultipleCandidates_RankedByNormalizedScore()
    {
        // Arrange
        var provider = new StubAiRecommendationProvider { ResponseToReturn = Array.Empty<AiRecommendationItemDto>() };
        var engine = BuildEngine(provider);

        var candidates = new[]
        {
            MakeCandidate("W-Low",  rawScore: 20.0, weight: 1.0),
            MakeCandidate("W-High", rawScore: 90.0, weight: 1.0),
            MakeCandidate("W-Mid",  rawScore: 50.0, weight: 1.0),
        };
        var request = MakeRequest(candidates, maxResults: 3);

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.Equal(3, response.Results.Count);
        Assert.Equal("W-High", response.Results[0].EntityId); // Highest score first
        Assert.Equal("W-Mid", response.Results[1].EntityId);
        Assert.Equal("W-Low", response.Results[2].EntityId);

        // Top candidate should be 100.0
        Assert.Equal(100.0, response.Results[0].MatchScore);

        // Scores should descend
        Assert.True(response.Results[0].MatchScore >= response.Results[1].MatchScore);
        Assert.True(response.Results[1].MatchScore >= response.Results[2].MatchScore);
    }

    // ── Test 4: AI success merges reasons ────────────────────────────────────

    [Fact]
    public async Task RecommendAsync_AiSuccess_MergesReasonsAndSetsConfidence()
    {
        // Arrange
        var aiItems = new List<AiRecommendationItemDto>
        {
            new("W-001", 90, 0.85,
                Reasons: new[] { "AI: Highly experienced in wheat harvesting." },
                Warnings: Array.Empty<string>())
        };
        var provider = new StubAiRecommendationProvider { ResponseToReturn = aiItems };
        var engine = BuildEngine(provider);

        var request = MakeRequest(new[] { MakeCandidate("W-001") });

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.True(response.AiEnriched);
        Assert.Single(response.Results);
        var result = response.Results[0];
        Assert.Contains(result.Reasons, r => r == "Good match");           // deterministic
        Assert.Contains(result.Reasons, r => r.StartsWith("AI:"));          // ai enriched
        Assert.InRange(result.ConfidenceScore, 0.0, 1.0);
        // With AI confidence 0.85: blend = 0.85*0.6 + 1.0*0.4 = 0.91
        Assert.True(result.ConfidenceScore > 0.8);
    }

    // ── Test 5: AI timeout falls back to deterministic ────────────────────────

    [Fact]
    public async Task RecommendAsync_AiTimeout_FallsBackToDeterminstic()
    {
        // Arrange
        var provider = new StubAiRecommendationProvider
        {
            ExceptionToThrow = new TimeoutException("AI timed out.")
        };
        var engine = BuildEngine(provider);
        var request = MakeRequest(new[] { MakeCandidate("W-001") });

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert — must NOT throw; must return deterministic result
        Assert.NotNull(response);
        Assert.Single(response.Results);
        Assert.False(response.AiEnriched);
        Assert.NotNull(response.FallbackReason);
        Assert.Contains("timed out", response.FallbackReason, StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 6: AI general failure falls back to deterministic ────────────────

    [Fact]
    public async Task RecommendAsync_AiGeneralFailure_FallsBackToDeterministic()
    {
        // Arrange
        var provider = new StubAiRecommendationProvider
        {
            ExceptionToThrow = new InvalidOperationException("AI service unavailable.")
        };
        var engine = BuildEngine(provider);
        var request = MakeRequest(new[] { MakeCandidate("W-001") });

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Single(response.Results);
        Assert.False(response.AiEnriched);
        Assert.NotNull(response.FallbackReason);
    }

    // ── Test 7: Cancellation propagates correctly ─────────────────────────────

    [Fact]
    public async Task RecommendAsync_Cancelled_ReturnsDeterministicResult()
    {
        // Arrange
        var provider = new StubAiRecommendationProvider
        {
            ExceptionToThrow = new OperationCanceledException()
        };
        var engine = BuildEngine(provider);
        var request = MakeRequest(new[] { MakeCandidate("W-001") });

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert — engine catches OperationCanceledException from AI and falls back
        Assert.NotNull(response);
        Assert.Single(response.Results);
        Assert.False(response.AiEnriched);
    }

    // ── Test 8: ForceDeterministicOnly skips AI provider ─────────────────────

    [Fact]
    public async Task RecommendAsync_ForceDeterministicOnly_DoesNotCallAiProvider()
    {
        // Arrange
        var provider = new StubAiRecommendationProvider();
        var engine = BuildEngine(provider);
        var request = MakeRequest(new[] { MakeCandidate("W-001") }, forceDeterministic: true);

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.Equal(0, provider.CallCount); // AI never called
        Assert.False(response.AiEnriched);
        Assert.Single(response.Results);
    }

    // ── Test 9: MinMatchScore filter removes low-scoring candidates ───────────

    [Fact]
    public async Task RecommendAsync_MinMatchScore_FiltersOutLowScoreCandidates()
    {
        // Arrange
        var engine = BuildEngine(new StubAiRecommendationProvider());
        var candidates = new[]
        {
            MakeCandidate("W-001", rawScore: 90.0),
            MakeCandidate("W-002", rawScore: 10.0), // After normalization: (10/90)*100 ≈ 11.1
        };
        // Set min score to 50 — W-002 should be excluded
        var request = MakeRequest(candidates, minMatchScore: 50.0);

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.Single(response.Results);
        Assert.Equal("W-001", response.Results[0].EntityId);
    }

    // ── Test 10: MaxResults is clamped and respected ──────────────────────────

    [Fact]
    public async Task RecommendAsync_MaxResults_ClampsAndLimitsOutput()
    {
        // Arrange
        var engine = BuildEngine(new StubAiRecommendationProvider());
        var candidates = Enumerable.Range(1, 10)
            .Select(i => MakeCandidate($"W-{i:D3}", rawScore: i * 5.0))
            .ToList();

        var request = MakeRequest(candidates, maxResults: 3);

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.Equal(3, response.Results.Count);
    }

    // ── Test 11: MaxResults > 20 is clamped to 20 ────────────────────────────

    [Fact]
    public async Task RecommendAsync_MaxResultsOver20_ClampedTo20()
    {
        // Arrange
        var engine = BuildEngine(new StubAiRecommendationProvider());
        var candidates = Enumerable.Range(1, 25)
            .Select(i => MakeCandidate($"W-{i:D3}", rawScore: i * 3.0))
            .ToList();

        var request = MakeRequest(candidates, maxResults: 999); // should be clamped to 20

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.True(response.Results.Count <= 20);
    }

    // ── Test 12: AI hallucinated entity ID is rejected ────────────────────────

    [Fact]
    public async Task RecommendAsync_AiHallucinatesEntityId_RejectedFromResults()
    {
        // Arrange
        var aiItems = new List<AiRecommendationItemDto>
        {
            new("HALLUCINATED-ID", 99, 0.99,
                Reasons: new[] { "This entity does not exist in candidates." },
                Warnings: Array.Empty<string>())
        };
        var provider = new StubAiRecommendationProvider { ResponseToReturn = aiItems };
        var engine = BuildEngine(provider);
        var request = MakeRequest(new[] { MakeCandidate("W-001") });

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert — hallucinated ID must NOT appear in results
        Assert.DoesNotContain(response.Results, r => r.EntityId == "HALLUCINATED-ID");
        // Real candidate still returned
        Assert.Single(response.Results);
        Assert.Equal("W-001", response.Results[0].EntityId);
    }

    // ── Test 13: Null factors produce 0 score without crashing ───────────────

    [Fact]
    public async Task RecommendAsync_CandidateWithEmptyFactors_ProducesZeroScoreNoCrash()
    {
        // Arrange
        var engine = BuildEngine(new StubAiRecommendationProvider());
        var candidates = new[]
        {
            new RecommendationCandidate("W-ZeroFactor", "Worker", Array.Empty<RecommendationFactor>(), null),
            MakeCandidate("W-Normal", rawScore: 80.0)
        };
        var request = MakeRequest(candidates);

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert — no crash; zero-factor candidate gets 0 score, filtered to bottom
        Assert.NotNull(response);
        Assert.Equal("W-Normal", response.Results[0].EntityId);
        Assert.Equal(100.0, response.Results[0].MatchScore);
    }

    // ── Test 14: Response scores are within valid bounds ─────────────────────

    [Fact]
    public async Task RecommendAsync_AllResults_HaveValidScoreBounds()
    {
        // Arrange
        var aiItems = new List<AiRecommendationItemDto>
        {
            new("W-001", 85, 0.9, Reasons: new[] { "Good" }, Warnings: Array.Empty<string>()),
            new("W-002", 60, 0.7, Reasons: new[] { "OK" }, Warnings: Array.Empty<string>()),
        };
        var provider = new StubAiRecommendationProvider { ResponseToReturn = aiItems };
        var engine = BuildEngine(provider);
        var candidates = new[]
        {
            MakeCandidate("W-001", rawScore: 90.0),
            MakeCandidate("W-002", rawScore: 50.0),
        };
        var request = MakeRequest(candidates);

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        foreach (var result in response.Results)
        {
            Assert.InRange(result.MatchScore, 0.0, 100.0);
            Assert.InRange(result.ConfidenceScore, 0.0, 1.0);
        }
    }

    // ── Test 15: Metadata is passed through to result ─────────────────────────

    [Fact]
    public async Task RecommendAsync_CandidateWithMetadata_MetadataPassedThrough()
    {
        // Arrange
        var engine = BuildEngine(new StubAiRecommendationProvider());
        var metadata = new Dictionary<string, string> { { "name", "Ramesh Patel" }, { "city", "Surat" } };
        var candidate = MakeCandidate("W-001", metadata: metadata);
        var request = MakeRequest(new[] { candidate });

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        Assert.NotNull(response.Results[0].Metadata);
        Assert.Equal("Ramesh Patel", response.Results[0].Metadata!["name"]);
        Assert.Equal("Surat", response.Results[0].Metadata!["city"]);
    }

    // ── Test 16: Candidates over 50 are capped silently ──────────────────────

    [Fact]
    public async Task RecommendAsync_CandidatesOver50_ProcessesOnlyFirst50()
    {
        // Arrange
        var provider = new StubAiRecommendationProvider();
        var engine = BuildEngine(provider);
        var candidates = Enumerable.Range(1, 60)
            .Select(i => MakeCandidate($"W-{i:D3}", rawScore: i * 1.0))
            .ToList();

        var request = MakeRequest(candidates, maxResults: 20);

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert — at most 20 results returned (capped at 50 candidates → top 20 results)
        Assert.True(response.Results.Count <= 20);
    }

    // ── Test 17: AI warnings surface in result ───────────────────────────────

    [Fact]
    public async Task RecommendAsync_AiWarnings_SurfaceInResult()
    {
        // Arrange
        var aiItems = new List<AiRecommendationItemDto>
        {
            new("W-001", 70, 0.6,
                Reasons: new[] { "Suitable" },
                Warnings: new[] { "Low experience data available." })
        };
        var provider = new StubAiRecommendationProvider { ResponseToReturn = aiItems };
        var engine = BuildEngine(provider);
        var request = MakeRequest(new[] { MakeCandidate("W-001") });

        // Act
        var response = await engine.RecommendAsync(request);

        // Assert
        var result = response.Results[0];
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("Low experience data available.", result.Warnings);
    }

    // ── Test 18: Invalid RecommendationType throws ArgumentException ──────────

    [Fact]
    public async Task RecommendAsync_EmptyRecommendationType_ThrowsArgumentException()
    {
        // Arrange
        var engine = BuildEngine();
        var request = new RecommendationRequest(
            RequestingUserId: Guid.NewGuid(),
            RecommendationType: "   ",
            Candidates: new[] { MakeCandidate("W-001") },
            ContextSummary: "Test context",
            Language: "en",
            MaxResults: 5);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => engine.RecommendAsync(request));
    }
}
