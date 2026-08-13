using FarmKart.Application.Abstractions.Farmer;
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

public sealed class FarmerAssignmentService : IFarmerAssignmentService
{
    private readonly FarmKartDbContext _dbContext;

    public FarmerAssignmentService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FarmerWorkerAssignmentResponse>> GetAssignmentsForJobAsync(Guid userId, Guid jobId)
    {
        var farmerProfile = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.UserId == userId);

        if (farmerProfile is null)
        {
            throw new ProfileNotFoundException("Farmer profile not found.");
        }

        var jobExists = await _dbContext.Jobs
            .AsNoTracking()
            .AnyAsync(j => j.Id == jobId && j.FarmerProfileId == farmerProfile.Id);

        if (!jobExists)
        {
            throw new JobNotFoundException("Job not found.");
        }

        await AssignmentBackfillSeeder.SyncAcceptedAssignmentsAsync(_dbContext);

        var assignments = await _dbContext.WorkerAssignments
            .AsNoTracking()
            .Include(a => a.Job)
            .Include(a => a.WorkerProfile)
                .ThenInclude(w => w.WorkerSkills)
                    .ThenInclude(ws => ws.Skill)
            .Where(a => a.JobId == jobId)
            .OrderByDescending(a => a.AssignedAtUtc)
            .ToListAsync();

        return assignments.Select(a =>
        {
            var skills = a.WorkerProfile?.WorkerSkills?
                .Select(ws => ws.Skill?.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList() ?? [];

            return new FarmerWorkerAssignmentResponse(
                AssignmentId: a.Id,
                JobId: a.JobId,
                JobTitle: a.Job?.Title ?? string.Empty,
                WorkerProfileId: a.WorkerProfileId,
                WorkerName: a.WorkerProfile?.FullName ?? "Worker",
                WorkerPhone: a.WorkerProfile?.Phone ?? string.Empty,
                WorkerExperienceYears: a.WorkerProfile?.ExperienceYears ?? 0,
                WorkerSkills: skills,
                StartDate: a.StartDate,
                EndDate: a.EndDate,
                AssignedAtUtc: a.AssignedAtUtc,
                Status: a.Status
            );
        }).ToList();
    }
}
