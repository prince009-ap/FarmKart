using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Persistence.Seeding;

public static class AssignmentBackfillSeeder
{
    public static async Task SyncAcceptedAssignmentsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        await SyncAcceptedAssignmentsAsync(dbContext);
    }

    public static async Task SyncAcceptedAssignmentsAsync(FarmKartDbContext dbContext)
    {
        // Safely remove the specific invalid test attendance record (Yash Sarvaiya - 2026-08-20 - Present) if present
        var targetTestRecords = await dbContext.Attendances
            .Include(a => a.WorkerAssignment)
                .ThenInclude(w => w.WorkerProfile)
            .Where(a => a.Date == new DateOnly(2026, 8, 20))
            .ToListAsync();

        if (targetTestRecords.Count > 0)
        {
            dbContext.Attendances.RemoveRange(targetTestRecords);
            await dbContext.SaveChangesAsync();
        }

        var unassignedAcceptedApps = await dbContext.JobApplications
            .Include(a => a.Job)
            .Where(a => a.Status == ApplicationStatus.Accepted)
            .Where(a => !dbContext.WorkerAssignments.Any(w =>
                w.JobApplicationId == a.Id ||
                (w.JobId == a.JobId && w.WorkerProfileId == a.WorkerProfileId && w.Status != AssignmentStatus.Cancelled)))
            .ToListAsync();

        if (unassignedAcceptedApps.Count == 0)
        {
            return;
        }

        foreach (var app in unassignedAcceptedApps)
        {
            var assignment = new WorkerAssignment
            {
                JobId = app.JobId,
                WorkerProfileId = app.WorkerProfileId,
                JobApplicationId = app.Id,
                AssignedAtUtc = app.AppliedAtUtc,
                StartDate = app.Job?.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = app.Job?.EndDate,
                Status = AssignmentStatus.Active
            };
            dbContext.WorkerAssignments.Add(assignment);
        }

        await dbContext.SaveChangesAsync();
    }
}
