using FarmKart.Application.Abstractions.Worker;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class WorkerProfileCompletionService : IWorkerProfileCompletionService
{
    private readonly FarmKartDbContext _dbContext;

    public WorkerProfileCompletionService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkerProfileCompletionResponse> GetProfileCompletionAsync(Guid workerUserId)
    {
        var profile = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .Include(w => w.WorkerSkills)
            .SingleOrDefaultAsync(w => w.UserId == workerUserId);

        if (profile is null)
        {
            throw new ProfileNotFoundException("Worker profile not found.");
        }

        var sections = new List<ProfileCompletionSectionResponse>();

        // 1. Basic Info (20%)
        var hasFullName = !string.IsNullOrWhiteSpace(profile.FullName);
        var hasPhone = !string.IsNullOrWhiteSpace(profile.Phone);
        var hasAddress = !string.IsNullOrWhiteSpace(profile.AddressInfo?.AddressLine) || !string.IsNullOrWhiteSpace(profile.AddressInfo?.City);
        var isBasicComplete = hasFullName && hasPhone && hasAddress;
        var basicPct = isBasicComplete ? 20 : (hasFullName && hasPhone ? 13 : (hasFullName ? 7 : 0));

        sections.Add(new ProfileCompletionSectionResponse(
            SectionKey: "basic_info",
            SectionName: "Basic Information",
            IsComplete: isBasicComplete,
            CompletionPercentage: basicPct,
            Description: "Full Name, Phone Number, and Address",
            ActionRoute: "/worker/profile"
        ));

        // 2. Skills & Experience (25%)
        var hasSkills = profile.WorkerSkills != null && profile.WorkerSkills.Count > 0;
        var hasExpDesc = !string.IsNullOrWhiteSpace(profile.ExperienceDescription);
        var isSkillsExpComplete = hasSkills && hasExpDesc;
        var skillsExpPct = (hasSkills ? 15 : 0) + (hasExpDesc ? 10 : 0);

        sections.Add(new ProfileCompletionSectionResponse(
            SectionKey: "skills_experience",
            SectionName: "Skills & Experience",
            IsComplete: isSkillsExpComplete,
            CompletionPercentage: skillsExpPct,
            Description: "List of farm work skills and experience description",
            ActionRoute: "/worker/profile"
        ));

        // 3. Availability (20%)
        var isAvailComplete = profile.IsAvailable;
        var availPct = isAvailComplete ? 20 : 10;

        sections.Add(new ProfileCompletionSectionResponse(
            SectionKey: "availability",
            SectionName: "Availability Status",
            IsComplete: isAvailComplete,
            CompletionPercentage: availPct,
            Description: "Availability status, date, and notes",
            ActionRoute: "/worker/profile"
        ));

        // 4. Job Preferences (25%)
        var hasPrefCat = !string.IsNullOrWhiteSpace(profile.PreferredWorkCategories);
        var hasPrefLoc = !string.IsNullOrWhiteSpace(profile.PreferredLocations);
        var hasMinWage = profile.MinimumDailyWage > 0;
        var isPrefComplete = hasPrefCat && hasPrefLoc && hasMinWage;
        var prefPct = (hasPrefCat ? 10 : 0) + (hasPrefLoc ? 10 : 0) + (hasMinWage ? 5 : 0);

        sections.Add(new ProfileCompletionSectionResponse(
            SectionKey: "job_preferences",
            SectionName: "Job Preferences",
            IsComplete: isPrefComplete,
            CompletionPercentage: prefPct,
            Description: "Preferred work categories, locations, and minimum daily wage",
            ActionRoute: "/worker/preferences"
        ));

        // 5. Profile Photo (10%)
        var hasPhoto = !string.IsNullOrWhiteSpace(profile.ProfileImageUrl);
        var isPhotoComplete = hasPhoto;
        var photoPct = hasPhoto ? 10 : 0;

        sections.Add(new ProfileCompletionSectionResponse(
            SectionKey: "profile_photo",
            SectionName: "Profile Photo",
            IsComplete: isPhotoComplete,
            CompletionPercentage: photoPct,
            Description: "Profile photo URL for farmer recognition",
            ActionRoute: "/worker/profile"
        ));

        var overallPct = Math.Min(100, basicPct + skillsExpPct + availPct + prefPct + photoPct);
        var verificationStatus = profile.VerificationStatus ?? "Not Verified";

        return new WorkerProfileCompletionResponse(
            OverallCompletionPercentage: overallPct,
            VerificationStatus: verificationStatus,
            Sections: sections
        );
    }
}
