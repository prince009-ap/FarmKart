using FarmKart.Application.Abstractions.Worker;
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

public sealed class WorkerJobService : IWorkerJobService
{
    private readonly FarmKartDbContext _dbContext;

    public WorkerJobService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WorkerAvailableJobResponse>> GetAvailableJobsAsync(Guid userId)
    {
        var workerProfile = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.UserId == userId);

        var appliedJobIds = new HashSet<Guid>();
        if (workerProfile != null)
        {
            var ids = await _dbContext.JobApplications
                .AsNoTracking()
                .Where(a => a.WorkerProfileId == workerProfile.Id)
                .Select(a => a.JobId)
                .ToListAsync();
            appliedJobIds = [.. ids];
        }

        var openJobs = await _dbContext.Jobs
            .AsNoTracking()
            .Include(j => j.FarmerProfile)
            .Where(j => j.Status == JobStatus.Open)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync();

        return openJobs.Select(j => ToAvailableJobResponse(j, appliedJobIds.Contains(j.Id))).ToList();
    }

    public async Task<WorkerAvailableJobResponse> GetAvailableJobDetailsAsync(Guid userId, Guid jobId)
    {
        var job = await _dbContext.Jobs
            .AsNoTracking()
            .Include(j => j.FarmerProfile)
            .SingleOrDefaultAsync(j => j.Id == jobId && j.Status == JobStatus.Open);

        if (job is null)
        {
            throw new JobNotFoundException();
        }

        var workerProfile = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.UserId == userId);

        var hasApplied = false;
        if (workerProfile != null)
        {
            hasApplied = await _dbContext.JobApplications
                .AsNoTracking()
                .AnyAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfile.Id);
        }

        return ToAvailableJobResponse(job, hasApplied);
    }

    public async Task<WorkerJobApplicationResponse> ApplyToJobAsync(Guid userId, Guid jobId, ApplyJobRequest? request)
    {
        var workerProfile = await _dbContext.WorkerProfiles
            .SingleOrDefaultAsync(w => w.UserId == userId);

        if (workerProfile is null)
        {
            throw new ProfileNotFoundException("Worker profile not found.");
        }

        var job = await _dbContext.Jobs
            .SingleOrDefaultAsync(j => j.Id == jobId && j.Status == JobStatus.Open);

        if (job is null)
        {
            throw new JobNotFoundException("This job is not available for application.");
        }

        var alreadyApplied = await _dbContext.JobApplications
            .AnyAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfile.Id);

        if (alreadyApplied)
        {
            throw new DuplicateApplicationException();
        }

        var application = new JobApplication
        {
            JobId = jobId,
            WorkerProfileId = workerProfile.Id,
            Status = ApplicationStatus.Pending,
            AppliedAtUtc = DateTime.UtcNow,
            Message = request?.Message?.Trim()
        };

        _dbContext.JobApplications.Add(application);
        await _dbContext.SaveChangesAsync();

        return new WorkerJobApplicationResponse(
            ApplicationId: application.Id,
            JobId: job.Id,
            JobTitle: job.Title,
            WorkCategory: job.WorkCategory,
            WagePerDay: job.WagePerDay,
            StartDate: job.StartDate,
            EndDate: job.EndDate,
            FarmLocation: job.FarmLocation,
            Status: application.Status,
            AppliedAtUtc: application.AppliedAtUtc,
            Message: application.Message
        );
    }

    public async Task<IReadOnlyList<WorkerJobApplicationResponse>> GetMyApplicationsAsync(Guid userId)
    {
        var workerProfile = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.UserId == userId);

        if (workerProfile is null)
        {
            return [];
        }

        var applications = await _dbContext.JobApplications
            .AsNoTracking()
            .Include(a => a.Job)
            .Where(a => a.WorkerProfileId == workerProfile.Id)
            .OrderByDescending(a => a.AppliedAtUtc)
            .ToListAsync();

        return applications.Select(a => new WorkerJobApplicationResponse(
            ApplicationId: a.Id,
            JobId: a.JobId,
            JobTitle: a.Job.Title,
            WorkCategory: a.Job.WorkCategory,
            WagePerDay: a.Job.WagePerDay,
            StartDate: a.Job.StartDate,
            EndDate: a.Job.EndDate,
            FarmLocation: a.Job.FarmLocation,
            Status: a.Status,
            AppliedAtUtc: a.AppliedAtUtc,
            Message: a.Message
        )).ToList();
    }

    private static WorkerAvailableJobResponse ToAvailableJobResponse(Job job, bool hasApplied) => new(
        Id: job.Id,
        Title: job.Title,
        Description: job.Description,
        WorkCategory: job.WorkCategory,
        CropType: job.CropType,
        WorkersRequired: job.WorkersRequired,
        RequiredExperience: job.RequiredExperience,
        WagePerDay: job.WagePerDay,
        StartDate: job.StartDate,
        EndDate: job.EndDate,
        WorkingHours: job.WorkingHours,
        FarmLocation: job.FarmLocation,
        FarmSize: job.FarmSize,
        FoodProvided: job.FoodProvided,
        AccommodationProvided: job.AccommodationProvided,
        IsUrgent: job.IsUrgent,
        Status: job.Status,
        CreatedAtUtc: job.CreatedAtUtc,
        HasApplied: hasApplied,
        FarmerName: job.FarmerProfile?.FullName ?? "Farmer"
    );
}
