using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using FarmKart.Application.Abstractions.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class FarmerJobService : IFarmerJobService
{
    private readonly FarmKartDbContext dbContext;
    private readonly INotificationService notificationService;

    public FarmerJobService(FarmKartDbContext dbContext, INotificationService notificationService)
    {
        this.dbContext = dbContext;
        this.notificationService = notificationService;
    }

    public async Task<IReadOnlyList<FarmerJobResponse>> GetJobsAsync(Guid userId) =>
        await dbContext.Jobs.AsNoTracking()
            .Where(job => job.FarmerProfile.UserId == userId)
            .OrderByDescending(job => job.CreatedAtUtc)
            .Select(job => ToResponse(job))
            .ToListAsync();

    public async Task<FarmerJobResponse> GetJobAsync(Guid userId, Guid jobId)
    {
        var job = await FindOwnedJobAsync(userId, jobId, asNoTracking: true);
        return ToResponse(job);
    }

    public async Task<FarmerJobResponse> CreateJobAsync(Guid userId, CreateFarmerJobRequest request)
    {
        ValidateDates(request.StartDate, request.EndDate);
        var farmer = await dbContext.FarmerProfiles.SingleOrDefaultAsync(profile => profile.UserId == userId)
            ?? throw new ProfileNotFoundException();

        var job = new Job { FarmerProfileId = farmer.Id, Status = JobStatus.Open };
        Apply(job, request.Title, request.Description, request.WorkCategory, request.CropType, request.WorkersRequired,
            request.RequiredExperience, request.WagePerDay, request.StartDate, request.EndDate, request.WorkingHours,
            request.FarmLocation, request.FarmSize, request.FoodProvided, request.AccommodationProvided, request.IsUrgent);
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();
        return ToResponse(job);
    }

    public async Task<FarmerJobResponse> UpdateJobAsync(Guid userId, Guid jobId, UpdateFarmerJobRequest request)
    {
        ValidateDates(request.StartDate, request.EndDate);
        var job = await FindOwnedJobAsync(userId, jobId);
        if (job.Status is not (JobStatus.Draft or JobStatus.Open))
            throw new InvalidOperationException("Only draft or open jobs can be edited.");

        Apply(job, request.Title, request.Description, request.WorkCategory, request.CropType, request.WorkersRequired,
            request.RequiredExperience, request.WagePerDay, request.StartDate, request.EndDate, request.WorkingHours,
            request.FarmLocation, request.FarmSize, request.FoodProvided, request.AccommodationProvided, request.IsUrgent);
        await dbContext.SaveChangesAsync();
        return ToResponse(job);
    }

    public async Task CancelJobAsync(Guid userId, Guid jobId)
    {
        var job = await FindOwnedJobAsync(userId, jobId);
        if (job.Status is JobStatus.Completed or JobStatus.Cancelled)
            throw new InvalidOperationException("This job cannot be cancelled.");

        job.Status = JobStatus.Cancelled;
        await dbContext.SaveChangesAsync();

        // Notify assigned and applicant workers
        var workerUserIds = await dbContext.JobApplications
            .AsNoTracking()
            .Where(a => a.JobId == jobId && a.WorkerProfile != null)
            .Select(a => a.WorkerProfile.UserId.ToString())
            .Distinct()
            .ToListAsync();

        foreach (var wUserId in workerUserIds)
        {
            try
            {
                await notificationService.CreateNotificationAsync(
                    wUserId,
                    "Job Cancelled",
                    $"The job '{job.Title}' has been cancelled.",
                    NotificationType.Job,
                    job.Id
                );
            }
            catch { }
        }
    }

    private async Task<Job> FindOwnedJobAsync(Guid userId, Guid jobId, bool asNoTracking = false)
    {
        var query = asNoTracking ? dbContext.Jobs.AsNoTracking() : dbContext.Jobs;
        return await query.SingleOrDefaultAsync(job => job.Id == jobId && job.FarmerProfile.UserId == userId)
            ?? throw new JobNotFoundException();
    }

    private static void ValidateDates(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate) throw new ArgumentException("EndDate must be on or after StartDate.");
    }

    private static void Apply(Job job, string title, string description, string workCategory, string? cropType,
        int workersRequired, int requiredExperience, decimal wagePerDay, DateOnly startDate, DateOnly endDate,
        string workingHours, string farmLocation, decimal? farmSize, bool foodProvided, bool accommodationProvided, bool isUrgent)
    {
        job.Title = title.Trim(); job.Description = description.Trim(); job.WorkCategory = workCategory.Trim();
        job.CropType = string.IsNullOrWhiteSpace(cropType) ? null : cropType.Trim(); job.WorkersRequired = workersRequired;
        job.RequiredExperience = requiredExperience; job.WagePerDay = wagePerDay; job.StartDate = startDate; job.EndDate = endDate;
        job.WorkingHours = workingHours.Trim(); job.FarmLocation = farmLocation.Trim(); job.FarmSize = farmSize;
        job.FoodProvided = foodProvided; job.AccommodationProvided = accommodationProvided; job.IsUrgent = isUrgent;
    }

    private static FarmerJobResponse ToResponse(Job job) => new(job.Id, job.Title, job.Description, job.WorkCategory,
        job.CropType, job.WorkersRequired, job.RequiredExperience, job.WagePerDay, job.StartDate, job.EndDate,
        job.WorkingHours, job.FarmLocation, job.FarmSize, job.FoodProvided, job.AccommodationProvided, job.IsUrgent,
        job.Status, job.CreatedAtUtc);
}
