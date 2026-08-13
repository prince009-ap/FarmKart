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

public sealed class WorkerAttendanceService : IWorkerAttendanceService
{
    private readonly FarmKartDbContext _dbContext;

    public WorkerAttendanceService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkerAttendanceSummaryResponse> GetMyAttendanceHistoryAsync(Guid userId)
    {
        var workerProfile = await GetWorkerProfileAsync(userId);

        var list = await _dbContext.Attendances
            .AsNoTracking()
            .Include(a => a.WorkerAssignment)
                .ThenInclude(w => w.Job)
                    .ThenInclude(j => j.FarmerProfile)
            .Where(a => a.WorkerAssignment.WorkerProfileId == workerProfile.Id)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        return CalculateSummary(list);
    }

    public async Task<WorkerAttendanceSummaryResponse> GetAssignmentAttendanceAsync(Guid userId, Guid assignmentId)
    {
        var workerProfile = await GetWorkerProfileAsync(userId);

        var assignment = await _dbContext.WorkerAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == assignmentId && a.WorkerProfileId == workerProfile.Id);

        if (assignment is null)
        {
            throw new JobNotFoundException("Assignment not found.");
        }

        var list = await _dbContext.Attendances
            .AsNoTracking()
            .Include(a => a.WorkerAssignment)
                .ThenInclude(w => w.Job)
                    .ThenInclude(j => j.FarmerProfile)
            .Where(a => a.WorkerAssignmentId == assignmentId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        return CalculateSummary(list);
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

    private static WorkerAttendanceSummaryResponse CalculateSummary(List<Attendance> list)
    {
        var total = list.Count;
        var present = list.Count(a => a.Status == AttendanceStatus.Present);
        var absent = list.Count(a => a.Status == AttendanceStatus.Absent);
        var halfDay = list.Count(a => a.Status == AttendanceStatus.HalfDay);
        var leave = list.Count(a => a.Status == AttendanceStatus.Leave);

        var percentage = total > 0
            ? Math.Round(((decimal)present + (0.5m * halfDay)) / total * 100m, 1)
            : 0m;

        var history = list.Select(a => new WorkerAttendanceRecordResponse(
            AttendanceId: a.Id,
            WorkerAssignmentId: a.WorkerAssignmentId,
            JobId: a.WorkerAssignment?.JobId ?? Guid.Empty,
            JobTitle: a.WorkerAssignment?.Job?.Title ?? string.Empty,
            FarmerName: a.WorkerAssignment?.Job?.FarmerProfile?.FullName ?? a.WorkerAssignment?.Job?.FarmerProfile?.FarmName ?? "Farmer",
            Date: a.Date,
            Status: a.Status,
            Notes: a.Notes,
            TotalHours: a.TotalHours
        )).ToList();

        return new WorkerAttendanceSummaryResponse(
            TotalDays: total,
            PresentDays: present,
            AbsentDays: absent,
            HalfDays: halfDay,
            LeaveDays: leave,
            AttendancePercentage: percentage,
            History: history
        );
    }
}
