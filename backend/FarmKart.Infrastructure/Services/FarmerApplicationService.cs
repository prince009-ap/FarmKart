using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class FarmerApplicationService : IFarmerApplicationService
{
    private readonly FarmKartDbContext _dbContext;

    public FarmerApplicationService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FarmerJobApplicationResponse>> GetApplicationsForJobAsync(Guid userId, Guid jobId)
    {
        var farmerProfile = await GetFarmerProfileAsync(userId);
        var jobExists = await _dbContext.Jobs
            .AsNoTracking()
            .AnyAsync(j => j.Id == jobId && j.FarmerProfileId == farmerProfile.Id);

        if (!jobExists)
        {
            throw new JobNotFoundException("Job not found.");
        }

        var applications = await _dbContext.JobApplications
            .AsNoTracking()
            .Include(a => a.Job)
            .Include(a => a.WorkerProfile)
                .ThenInclude(w => w.WorkerSkills)
                    .ThenInclude(ws => ws.Skill)
            .Where(a => a.JobId == jobId)
            .OrderByDescending(a => a.AppliedAtUtc)
            .ToListAsync();

        return applications.Select(ToResponse).ToList();
    }

    public async Task<FarmerJobApplicationResponse> GetApplicationDetailsAsync(Guid userId, Guid applicationId)
    {
        var farmerProfile = await GetFarmerProfileAsync(userId);
        var application = await _dbContext.JobApplications
            .AsNoTracking()
            .Include(a => a.Job)
            .Include(a => a.WorkerProfile)
                .ThenInclude(w => w.WorkerSkills)
                    .ThenInclude(ws => ws.Skill)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.Job.FarmerProfileId == farmerProfile.Id);

        if (application is null)
        {
            throw new JobNotFoundException("Application not found.");
        }

        return ToResponse(application);
    }

    public async Task<FarmerJobApplicationResponse> AcceptApplicationAsync(Guid userId, Guid applicationId)
    {
        var farmerProfile = await GetFarmerProfileAsync(userId);
        var application = await _dbContext.JobApplications
            .Include(a => a.Job)
            .Include(a => a.WorkerProfile)
                .ThenInclude(w => w.WorkerSkills)
                    .ThenInclude(ws => ws.Skill)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.Job.FarmerProfileId == farmerProfile.Id);

        if (application is null)
        {
            throw new JobNotFoundException("Application not found.");
        }

        if (application.Status != ApplicationStatus.Pending)
        {
            throw new InvalidOperationException("Application is not pending.");
        }

        var currentAcceptedCount = await _dbContext.JobApplications
            .CountAsync(a => a.JobId == application.JobId && a.Status == ApplicationStatus.Accepted);

        var currentAssignmentsCount = await _dbContext.WorkerAssignments
            .CountAsync(a => a.JobId == application.JobId && a.Status != AssignmentStatus.Cancelled);

        if (currentAcceptedCount >= application.Job.WorkersRequired || currentAssignmentsCount >= application.Job.WorkersRequired)
        {
            throw new InvalidOperationException("Job worker capacity has been reached.");
        }

        var existingAssignment = await _dbContext.WorkerAssignments
            .AnyAsync(a => a.JobId == application.JobId && a.WorkerProfileId == application.WorkerProfileId && a.Status != AssignmentStatus.Cancelled);

        if (existingAssignment)
        {
            throw new InvalidOperationException("Worker is already assigned to this job.");
        }

        application.Status = ApplicationStatus.Accepted;

        var assignment = new WorkerAssignment
        {
            JobId = application.JobId,
            WorkerProfileId = application.WorkerProfileId,
            JobApplicationId = application.Id,
            AssignedAtUtc = DateTime.UtcNow,
            StartDate = application.Job.StartDate,
            EndDate = application.Job.EndDate,
            Status = AssignmentStatus.Active
        };

        _dbContext.WorkerAssignments.Add(assignment);
        await _dbContext.SaveChangesAsync();

        return ToResponse(application);
    }

    public async Task<FarmerJobApplicationResponse> RejectApplicationAsync(Guid userId, Guid applicationId)
    {
        var farmerProfile = await GetFarmerProfileAsync(userId);
        var application = await _dbContext.JobApplications
            .Include(a => a.Job)
            .Include(a => a.WorkerProfile)
                .ThenInclude(w => w.WorkerSkills)
                    .ThenInclude(ws => ws.Skill)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.Job.FarmerProfileId == farmerProfile.Id);

        if (application is null)
        {
            throw new JobNotFoundException("Application not found.");
        }

        if (application.Status != ApplicationStatus.Pending)
        {
            throw new InvalidOperationException("Application is not pending.");
        }

        application.Status = ApplicationStatus.Rejected;
        await _dbContext.SaveChangesAsync();

        return ToResponse(application);
    }

    private async Task<FarmerProfile> GetFarmerProfileAsync(Guid userId)
    {
        var profile = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.UserId == userId);

        if (profile is null)
        {
            throw new ProfileNotFoundException("Farmer profile not found.");
        }

        return profile;
    }

    private static FarmerJobApplicationResponse ToResponse(JobApplication application)
    {
        var skills = application.WorkerProfile?.WorkerSkills?
            .Select(ws => ws.Skill?.Name ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList() ?? [];

        return new FarmerJobApplicationResponse(
            ApplicationId: application.Id,
            JobId: application.JobId,
            JobTitle: application.Job?.Title ?? string.Empty,
            ApplicantWorkerId: application.WorkerProfileId,
            ApplicantName: application.WorkerProfile?.FullName ?? "Worker",
            ApplicantPhone: application.WorkerProfile?.Phone ?? string.Empty,
            ApplicantExperienceYears: application.WorkerProfile?.ExperienceYears ?? 0,
            ApplicantSkills: skills,
            Status: application.Status,
            AppliedAtUtc: application.AppliedAtUtc,
            Message: application.Message
        );
    }
}
