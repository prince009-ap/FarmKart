using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FarmKart.Infrastructure.Services.AI;

/// <summary>
/// AI Worker Recommendation service implementation (AI-02).
///
/// Integrates with AI-01 Recommendation Engine Foundation (<see cref="IRecommendationEngine"/>).
/// Pipeline:
///   1. Verify farmer profile and job ownership.
///   2. Query candidate worker profiles from database with eager loading.
///   3. Apply hard eligibility filtering (availability, active assignment conflicts, self-assignment).
///   4. Build deterministic weighted scoring factors (Skill, Experience, Availability, Wage, Rating, Location).
///   5. Pass candidates to <see cref="IRecommendationEngine.RecommendAsync"/> for ranking and AI reasoning enrichment.
///   6. Map output to <see cref="WorkerRecommendationResponse"/> without exposing sensitive data.
///   7. Zero database mutations occur during recommendation generation.
/// </summary>
public sealed class WorkerRecommendationService : IWorkerRecommendationService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly ILogger<WorkerRecommendationService> _logger;

    public WorkerRecommendationService(
        FarmKartDbContext dbContext,
        IRecommendationEngine recommendationEngine,
        ILogger<WorkerRecommendationService> logger)
    {
        _dbContext = dbContext;
        _recommendationEngine = recommendationEngine;
        _logger = logger;
    }

    public async Task<WorkerRecommendationResponse> GetRecommendedWorkersAsync(
        Guid farmerUserId,
        Guid jobId,
        int topN = 5,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Resolve Farmer Profile ──────────────────────────────────────────
        var farmerProfile = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == farmerUserId, cancellationToken);

        if (farmerProfile is null)
        {
            throw new ProfileNotFoundException("Farmer profile not found.");
        }

        // ── 2. Load Job & verify ownership ─────────────────────────────────────
        var job = await _dbContext.Jobs
            .AsNoTracking()
            .Include(j => j.JobApplications)
            .Include(j => j.WorkerAssignments)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.FarmerProfileId == farmerProfile.Id, cancellationToken);

        if (job is null)
        {
            throw new JobNotFoundException("Job not found or access denied.");
        }

        topN = Math.Clamp(topN, 1, 20);

        _logger.LogInformation(
            "Generating worker recommendations for JobId={JobId} FarmerUserId={FarmerUserId} TopN={TopN}",
            jobId, farmerUserId, topN);

        // ── 3. Fetch all active worker profiles from database ─────────────────
        var workers = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .Include(w => w.WorkerSkills)
                .ThenInclude(ws => ws.Skill)
            .Include(w => w.WorkerAssignments)
            .Include(w => w.JobApplications)
            .ToListAsync(cancellationToken);

        if (workers.Count == 0)
        {
            return new WorkerRecommendationResponse(
                JobId: job.Id,
                JobTitle: job.Title,
                WorkCategory: job.WorkCategory,
                Recommendations: Array.Empty<WorkerRecommendationItemDto>(),
                AiEnriched: false,
                FallbackReason: "No registered worker profiles found in system.");
        }

        // Fetch review ratings for all workers in batch
        var workerUserIds = workers.Select(w => w.UserId.ToString()).ToList();
        var reviewRatings = await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => workerUserIds.Contains(r.RevieweeUserId))
            .GroupBy(r => r.RevieweeUserId)
            .Select(g => new
            {
                UserId = g.Key,
                AverageRating = g.Average(r => (double)r.Rating),
                TotalReviews = g.Count()
            })
            .ToDictionaryAsync(x => x.UserId, x => (x.AverageRating, x.TotalReviews), cancellationToken);

        // ── 4. Apply Hard Eligibility Filtering ────────────────────────────────
        var eligibleWorkers = new List<WorkerProfile>();

        foreach (var worker in workers)
        {
            // Hard Filter 1: General availability toggle
            if (!worker.IsAvailable)
                continue;

            // Hard Filter 2: AvailableFrom date must be on or before job start date
            if (worker.AvailableFrom.HasValue && worker.AvailableFrom.Value > job.StartDate)
                continue;

            // Hard Filter 3: Check for active assignment date overlap
            bool hasConflictingAssignment = worker.WorkerAssignments.Any(a =>
                (a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.Pending) &&
                a.StartDate <= job.EndDate &&
                (a.EndDate == null || a.EndDate.Value >= job.StartDate));

            if (hasConflictingAssignment)
                continue;

            // Hard Filter 4: Exclude worker if already assigned to this job
            bool alreadyAssignedToThisJob = job.WorkerAssignments.Any(a =>
                a.WorkerProfileId == worker.Id &&
                (a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.Completed));

            if (alreadyAssignedToThisJob)
                continue;

            eligibleWorkers.Add(worker);
        }

        if (eligibleWorkers.Count == 0)
        {
            return new WorkerRecommendationResponse(
                JobId: job.Id,
                JobTitle: job.Title,
                WorkCategory: job.WorkCategory,
                Recommendations: Array.Empty<WorkerRecommendationItemDto>(),
                AiEnriched: false,
                FallbackReason: "No eligible workers available for the specified job dates or requirements.");
        }

        // ── 5. Build Scoring Factors for AI-01 Engine ────────────────────────
        var candidates = new List<RecommendationCandidate>();
        var workerMap = new Dictionary<string, WorkerProfile>(StringComparer.OrdinalIgnoreCase);

        foreach (var worker in eligibleWorkers)
        {
            var entityId = worker.Id.ToString();
            workerMap[entityId] = worker;

            var factors = new List<RecommendationFactor>();

            // Factor 1: Skill / Work Category Match (Weight: 0.25)
            var workerSkillNames = worker.WorkerSkills
                .Where(ws => ws.Skill != null)
                .Select(ws => ws.Skill.Name)
                .ToList();

            var preferredCategories = (worker.PreferredWorkCategories ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            double skillScore = CalculateSkillMatchScore(job, workerSkillNames, preferredCategories, out var skillReason);
            factors.Add(new RecommendationFactor("SkillMatch", skillScore, 0.25, skillReason));

            // Factor 2: Experience Match (Weight: 0.20)
            double expScore = CalculateExperienceScore(job.RequiredExperience, worker.ExperienceYears, out var expReason);
            factors.Add(new RecommendationFactor("ExperienceMatch", expScore, 0.20, expReason));

            // Factor 3: Availability Match (Weight: 0.15)
            double availScore = CalculateAvailabilityScore(job.StartDate, worker.AvailableFrom, out var availReason);
            factors.Add(new RecommendationFactor("AvailabilityMatch", availScore, 0.15, availReason));

            // Factor 4: Wage Compatibility (Weight: 0.15)
            double wageScore = CalculateWageScore(job.WagePerDay, worker.ExpectedDailyWage, worker.MinimumDailyWage, out var wageReason);
            factors.Add(new RecommendationFactor("WageCompatibility", wageScore, 0.15, wageReason));

            // Factor 5: Worker Rating (Weight: 0.15)
            reviewRatings.TryGetValue(worker.UserId.ToString(), out var ratingInfo);
            double ratingScore = CalculateRatingScore(ratingInfo.AverageRating, ratingInfo.TotalReviews, out var ratingReason);
            factors.Add(new RecommendationFactor("WorkerRating", ratingScore, 0.15, ratingReason));

            // Factor 6: Location Compatibility (Weight: 0.10)
            double locScore = CalculateLocationScore(job.FarmLocation, worker.AddressInfo.City, worker.AddressInfo.State, worker.PreferredLocations, out var locReason);
            factors.Add(new RecommendationFactor("LocationMatch", locScore, 0.10, locReason));

            // Non-sensitive metadata payload for AI reasoning context
            var metadata = new Dictionary<string, string>
            {
                { "name", worker.FullName },
                { "experienceYears", $"{worker.ExperienceYears} years" },
                { "expectedWage", $"₹{worker.ExpectedDailyWage}/day" },
                { "city", worker.AddressInfo.City ?? "Unknown" },
                { "skills", string.Join(", ", workerSkillNames.Take(4)) }
            };

            candidates.Add(new RecommendationCandidate(entityId, "Worker", factors, metadata));
        }

        // ── 6. Execute AI-01 Recommendation Engine ───────────────────────────
        var contextSummary = $"Job '{job.Title}' in category '{job.WorkCategory}' at '{job.FarmLocation}'. Requires {job.WorkersRequired} workers, {job.RequiredExperience} yrs exp, ₹{job.WagePerDay}/day from {job.StartDate} to {job.EndDate}.";

        var engineRequest = new RecommendationRequest(
            RequestingUserId: farmerUserId,
            RecommendationType: "Worker",
            Candidates: candidates,
            ContextSummary: contextSummary,
            Language: "en",
            MaxResults: topN,
            MinMatchScore: 0.0);

        var engineResponse = await _recommendationEngine.RecommendAsync(engineRequest, cancellationToken);

        // ── 7. Map Results to WorkerRecommendationResponse ───────────────────
        var recommendationItems = new List<WorkerRecommendationItemDto>();

        foreach (var res in engineResponse.Results)
        {
            if (!workerMap.TryGetValue(res.EntityId, out var worker))
                continue;

            reviewRatings.TryGetValue(worker.UserId.ToString(), out var ratingInfo);

            bool hasApplied = job.JobApplications.Any(ja => ja.WorkerProfileId == worker.Id);

            var skills = worker.WorkerSkills
                .Where(ws => ws.Skill != null)
                .Select(ws => ws.Skill.Name)
                .ToList();

            recommendationItems.Add(new WorkerRecommendationItemDto(
                WorkerProfileId: worker.Id,
                UserId: worker.UserId,
                FullName: worker.FullName,
                ProfileImageUrl: worker.ProfileImageUrl,
                ExperienceYears: worker.ExperienceYears,
                ExpectedDailyWage: worker.ExpectedDailyWage,
                City: worker.AddressInfo.City,
                State: worker.AddressInfo.State,
                AverageRating: Math.Round(ratingInfo.AverageRating, 1),
                TotalReviews: ratingInfo.TotalReviews,
                Skills: skills,
                HasApplied: hasApplied,
                MatchScore: res.MatchScore,
                ConfidenceScore: res.ConfidenceScore,
                Reasons: res.Reasons,
                Warnings: res.Warnings));
        }

        return new WorkerRecommendationResponse(
            JobId: job.Id,
            JobTitle: job.Title,
            WorkCategory: job.WorkCategory,
            Recommendations: recommendationItems,
            AiEnriched: engineResponse.AiEnriched,
            FallbackReason: engineResponse.FallbackReason);
    }

    // ── Scoring Factor Computation Helpers ─────────────────────────────────────

    private static double CalculateSkillMatchScore(
        Job job,
        List<string> workerSkillNames,
        string[] preferredCategories,
        out string reason)
    {
        bool exactSkill = workerSkillNames.Any(s =>
            s.Contains(job.WorkCategory, StringComparison.OrdinalIgnoreCase) ||
            job.WorkCategory.Contains(s, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(job.CropType) && s.Contains(job.CropType, StringComparison.OrdinalIgnoreCase)));

        if (exactSkill)
        {
            reason = $"Has skill relevant to {job.WorkCategory}";
            return 100.0;
        }

        bool prefCategoryMatch = preferredCategories.Any(pc =>
            pc.Contains(job.WorkCategory, StringComparison.OrdinalIgnoreCase) ||
            job.WorkCategory.Contains(pc, StringComparison.OrdinalIgnoreCase));

        if (prefCategoryMatch)
        {
            reason = $"Preferred work category matches {job.WorkCategory}";
            return 80.0;
        }

        if (workerSkillNames.Count > 0)
        {
            reason = $"Has {workerSkillNames.Count} general agricultural skills";
            return 50.0;
        }

        reason = "Basic skill profile";
        return 30.0;
    }

    private static double CalculateExperienceScore(int requiredExp, int workerExp, out string reason)
    {
        if (workerExp >= requiredExp)
        {
            reason = $"{workerExp} years experience (meets/exceeds {requiredExp} years required)";
            return 100.0;
        }

        if (requiredExp == 0)
        {
            reason = $"{workerExp} years experience (no minimum required)";
            return 100.0;
        }

        double ratio = (double)workerExp / requiredExp;
        double score = Math.Round(ratio * 100.0, 1);
        reason = $"{workerExp} years experience ({requiredExp} years required)";
        return Math.Clamp(score, 10.0, 95.0);
    }

    private static double CalculateAvailabilityScore(DateOnly jobStartDate, DateOnly? availableFrom, out string reason)
    {
        if (!availableFrom.HasValue)
        {
            reason = "Immediately available for work";
            return 100.0;
        }

        if (availableFrom.Value <= jobStartDate)
        {
            reason = $"Available from {availableFrom.Value:d MMM yyyy} (before job start date)";
            return 90.0;
        }

        reason = $"Available from {availableFrom.Value:d MMM yyyy}";
        return 40.0;
    }

    private static double CalculateWageScore(decimal jobWage, decimal expectedWage, decimal minWage, out string reason)
    {
        if (expectedWage <= 0)
            expectedWage = minWage;

        if (expectedWage <= jobWage)
        {
            reason = $"Expected wage ₹{expectedWage}/day is within job budget ₹{jobWage}/day";
            return 100.0;
        }

        if (expectedWage <= jobWage * 1.15m)
        {
            reason = $"Expected wage ₹{expectedWage}/day is slightly above job budget ₹{jobWage}/day";
            return 75.0;
        }

        if (expectedWage <= jobWage * 1.30m)
        {
            reason = $"Expected wage ₹{expectedWage}/day is 30% above budget ₹{jobWage}/day";
            return 50.0;
        }

        reason = $"Expected wage ₹{expectedWage}/day exceeds job budget ₹{jobWage}/day";
        return 20.0;
    }

    private static double CalculateRatingScore(double avgRating, int totalReviews, out string reason)
    {
        if (totalReviews == 0)
        {
            reason = "No reviews yet (new worker profile)";
            return 75.0; // Neutral score for new workers
        }

        if (avgRating >= 4.5)
        {
            reason = $"High rating: {avgRating:F1} ★ from {totalReviews} review(s)";
            return 100.0;
        }

        if (avgRating >= 4.0)
        {
            reason = $"Good rating: {avgRating:F1} ★ from {totalReviews} review(s)";
            return 85.0;
        }

        if (avgRating >= 3.5)
        {
            reason = $"Average rating: {avgRating:F1} ★ from {totalReviews} review(s)";
            return 70.0;
        }

        reason = $"Rating: {avgRating:F1} ★ from {totalReviews} review(s)";
        return 50.0;
    }

    private static double CalculateLocationScore(
        string farmLocation,
        string? city,
        string? state,
        string? preferredLocations,
        out string reason)
    {
        if (!string.IsNullOrWhiteSpace(city) && farmLocation.Contains(city, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Located in {city} (matches farm location)";
            return 100.0;
        }

        if (!string.IsNullOrWhiteSpace(preferredLocations) &&
            preferredLocations.Contains(farmLocation, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Preferred locations include farm location ({farmLocation})";
            return 90.0;
        }

        if (!string.IsNullOrWhiteSpace(state) && farmLocation.Contains(state, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Located in same state ({state})";
            return 70.0;
        }

        reason = "Location evaluation";
        return 40.0;
    }
}
