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

public sealed class WorkerEarningsService : IWorkerEarningsService
{
    private readonly FarmKartDbContext _dbContext;

    public WorkerEarningsService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkerEarningsSummaryResponse> GetWorkerEarningsAsync(Guid workerUserId)
    {
        var workerProfile = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.UserId == workerUserId);

        if (workerProfile is null)
        {
            throw new ProfileNotFoundException("Worker profile not found.");
        }

        var assignments = await _dbContext.WorkerAssignments
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.FarmerProfile)
            .Include(a => a.Attendances)
            .Where(a => a.WorkerProfileId == workerProfile.Id && a.Status != AssignmentStatus.Cancelled)
            .OrderByDescending(a => a.AssignedAtUtc)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);

        var history = new List<WorkerEarningsItemResponse>();
        decimal totalEarnings = 0m;
        decimal thisMonthEarnings = 0m;
        int completedJobsCount = 0;

        foreach (var assignment in assignments)
        {
            var job = assignment.Job;
            if (job is null) continue;

            var dailyWage = job.WagePerDay;
            decimal daysWorked = 0m;
            decimal earnedForAssignment = 0m;

            if (assignment.Attendances != null && assignment.Attendances.Count > 0)
            {
                foreach (var att in assignment.Attendances)
                {
                    if (att.Status == AttendanceStatus.Present)
                    {
                        daysWorked += 1.0m;
                        earnedForAssignment += dailyWage;
                    }
                    else if (att.Status == AttendanceStatus.HalfDay)
                    {
                        daysWorked += 0.5m;
                        earnedForAssignment += (dailyWage * 0.5m);
                    }
                }
            }
            else
            {
                var isCompletedOrFinished = assignment.Status == AssignmentStatus.Completed
                    || job.Status == JobStatus.Completed
                    || (assignment.EndDate.HasValue && assignment.EndDate.Value <= today)
                    || (job.EndDate <= today);

                if (isCompletedOrFinished)
                {
                    var start = assignment.StartDate;
                    var end = assignment.EndDate ?? job.EndDate;
                    var dayCount = Math.Max(1, (end.DayNumber - start.DayNumber) + 1);
                    daysWorked = dayCount;
                    earnedForAssignment = daysWorked * dailyWage;
                }
            }

            var statusStr = (assignment.Status == AssignmentStatus.Completed || job.Status == JobStatus.Completed || (assignment.EndDate.HasValue && assignment.EndDate.Value <= today) || job.EndDate <= today)
                ? "Completed"
                : "Active";

            if (earnedForAssignment > 0)
            {
                totalEarnings += earnedForAssignment;
                if (statusStr == "Completed")
                {
                    completedJobsCount++;
                }

                if (assignment.StartDate >= firstDayOfMonth)
                {
                    thisMonthEarnings += earnedForAssignment;
                }

                var farmerName = job.FarmerProfile?.FullName ?? job.FarmerProfile?.FarmName ?? "Farmer";

                history.Add(new WorkerEarningsItemResponse(
                    AssignmentId: assignment.Id,
                    JobId: job.Id,
                    JobTitle: job.Title,
                    FarmerName: farmerName,
                    WorkCategory: job.WorkCategory,
                    StartDate: assignment.StartDate,
                    EndDate: assignment.EndDate ?? job.EndDate,
                    DaysWorked: daysWorked,
                    DailyWage: dailyWage,
                    TotalEarned: earnedForAssignment,
                    Status: statusStr,
                    AssignedAtUtc: assignment.AssignedAtUtc
                ));
            }
        }

        return new WorkerEarningsSummaryResponse(
            TotalEarnings: totalEarnings,
            CompletedJobsCount: completedJobsCount,
            ThisMonthEarnings: thisMonthEarnings,
            AllTimeEarnings: totalEarnings,
            EarningsHistory: history
        );
    }
}
