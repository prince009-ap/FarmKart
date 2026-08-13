using FarmKart.Application.Abstractions.Worker;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class WorkerAssignmentService : IWorkerAssignmentService
{
    private readonly FarmKartDbContext _dbContext;

    public WorkerAssignmentService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WorkerAssignmentResponse>> GetMyAssignmentsAsync(Guid userId)
    {
        var workerProfile = await GetWorkerProfileAsync(userId);
        await AssignmentBackfillSeeder.SyncAcceptedAssignmentsAsync(_dbContext);

        var assignments = await _dbContext.WorkerAssignments
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.FarmerProfile)
            .Where(a => a.WorkerProfileId == workerProfile.Id)
            .OrderByDescending(a => a.AssignedAtUtc)
            .ToListAsync();

        return assignments.Select(ToResponse).ToList();
    }

    public async Task<WorkerAssignmentResponse> GetAssignmentDetailsAsync(Guid userId, Guid assignmentId)
    {
        var workerProfile = await GetWorkerProfileAsync(userId);

        var assignment = await _dbContext.WorkerAssignments
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.FarmerProfile)
            .SingleOrDefaultAsync(a => a.Id == assignmentId && a.WorkerProfileId == workerProfile.Id);

        if (assignment is null)
        {
            throw new JobNotFoundException("Assignment not found.");
        }

        return ToResponse(assignment);
    }

    private async Task<WorkerProfile> GetWorkerProfileAsync(Guid userId)
    {
        var profile = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.UserId == userId);

        if (profile is null)
        {
            throw new ProfileNotFoundException("Worker profile not found.");
        }

        return profile;
    }

    private static WorkerAssignmentResponse ToResponse(WorkerAssignment a)
    {
        return new WorkerAssignmentResponse(
            AssignmentId: a.Id,
            JobId: a.JobId,
            JobTitle: a.Job?.Title ?? string.Empty,
            WorkCategory: a.Job?.WorkCategory ?? string.Empty,
            WagePerDay: a.Job?.WagePerDay ?? 0,
            FarmerName: a.Job?.FarmerProfile?.FullName ?? a.Job?.FarmerProfile?.FarmName ?? "Farmer",
            FarmLocation: a.Job?.FarmLocation ?? string.Empty,
            WorkingHours: a.Job?.WorkingHours ?? string.Empty,
            StartDate: a.StartDate,
            EndDate: a.EndDate,
            AssignedAtUtc: a.AssignedAtUtc,
            Status: a.Status
        );
    }
}
