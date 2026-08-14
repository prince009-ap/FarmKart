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

public sealed class WorkerWorkHistoryService : IWorkerWorkHistoryService
{
    private readonly FarmKartDbContext _dbContext;

    public WorkerWorkHistoryService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkerWorkHistorySummaryResponse> GetWorkerWorkHistoryAsync(Guid workerUserId)
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
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignmentIds = assignments.Select(a => a.Id).ToList();

        var reviewsMap = await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.RelatedEntityType == ReviewEntityType.WorkerAssignment
                && r.RelatedEntityId.HasValue
                && assignmentIds.Contains(r.RelatedEntityId.Value))
            .ToDictionaryAsync(r => r.RelatedEntityId!.Value);

        var historyItems = new List<WorkerWorkHistoryItemResponse>();
        decimal totalWorkDays = 0m;
        decimal totalEarnings = 0m;
        int completedJobsCount = 0;

        foreach (var assignment in assignments)
        {
            var job = assignment.Job;
            if (job is null) continue;

            var isFinished = assignment.Status == AssignmentStatus.Completed
                || job.Status == JobStatus.Completed
                || (assignment.EndDate.HasValue && assignment.EndDate.Value <= today)
                || (job.EndDate <= today);

            if (!isFinished) continue;

            var dailyWage = job.WagePerDay;
            decimal daysWorked = 0m;
            decimal earnedForAssignment = 0m;
            int presentCount = 0;
            int halfDayCount = 0;

            if (assignment.Attendances != null && assignment.Attendances.Count > 0)
            {
                foreach (var att in assignment.Attendances)
                {
                    if (att.Status == AttendanceStatus.Present)
                    {
                        presentCount++;
                        daysWorked += 1.0m;
                        earnedForAssignment += dailyWage;
                    }
                    else if (att.Status == AttendanceStatus.HalfDay)
                    {
                        halfDayCount++;
                        daysWorked += 0.5m;
                        earnedForAssignment += (dailyWage * 0.5m);
                    }
                }
            }
            else
            {
                var start = assignment.StartDate;
                var end = assignment.EndDate ?? job.EndDate;
                var dayCount = Math.Max(1, (end.DayNumber - start.DayNumber) + 1);
                daysWorked = dayCount;
                earnedForAssignment = daysWorked * dailyWage;
            }

            int? rating = null;
            string? reviewComment = null;

            if (reviewsMap.TryGetValue(assignment.Id, out var review))
            {
                rating = review.Rating;
                reviewComment = review.Comment;
            }

            completedJobsCount++;
            totalWorkDays += daysWorked;
            totalEarnings += earnedForAssignment;

            var farmerName = job.FarmerProfile?.FullName ?? job.FarmerProfile?.FarmName ?? "Farmer";
            var location = job.FarmLocation;

            historyItems.Add(new WorkerWorkHistoryItemResponse(
                AssignmentId: assignment.Id,
                JobId: job.Id,
                JobTitle: job.Title,
                WorkCategory: job.WorkCategory,
                FarmerName: farmerName,
                Location: location,
                StartDate: assignment.StartDate,
                EndDate: assignment.EndDate ?? job.EndDate,
                DailyWage: dailyWage,
                DaysWorked: daysWorked,
                PresentCount: presentCount,
                HalfDayCount: halfDayCount,
                TotalEarned: earnedForAssignment,
                Rating: rating,
                ReviewComment: reviewComment,
                Status: "Completed",
                CompletedAtUtc: DateTime.UtcNow
            ));
        }

        return new WorkerWorkHistorySummaryResponse(
            TotalCompletedJobs: completedJobsCount,
            TotalWorkDays: totalWorkDays,
            TotalEarnings: totalEarnings,
            HistoryItems: historyItems
        );
    }
}
