using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FarmKart.Application.Abstractions.Notification;
using FarmKart.Domain.Enums;

namespace FarmKart.Infrastructure.Services;

public sealed class FarmerAttendanceService : IFarmerAttendanceService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public FarmerAttendanceService(FarmKartDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<FarmerAttendanceResponse>> GetJobAttendanceAsync(Guid userId, Guid jobId, DateOnly? date = null)
    {
        var farmerProfile = await GetFarmerProfileAsync(userId);
        await VerifyJobOwnershipAsync(farmerProfile.Id, jobId);

        var query = _dbContext.Attendances
            .AsNoTracking()
            .Include(a => a.WorkerAssignment)
                .ThenInclude(w => w.WorkerProfile)
            .Where(a => a.WorkerAssignment.JobId == jobId);

        if (date.HasValue)
        {
            query = query.Where(a => a.Date == date.Value);
        }

        var list = await query.OrderByDescending(a => a.Date).ThenBy(a => a.WorkerAssignment.WorkerProfile.FullName).ToListAsync();
        return list.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<FarmerAttendanceResponse>> SaveJobAttendanceAsync(Guid userId, Guid jobId, SaveJobAttendanceRequest request)
    {
        var farmerProfile = await GetFarmerProfileAsync(userId);
        
        var job = await _dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(j => j.Id == jobId && j.FarmerProfileId == farmerProfile.Id);

        if (job is null)
        {
            throw new JobNotFoundException("Job not found.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.Date > today)
        {
            throw new InvalidOperationException("Attendance cannot be recorded for a future date.");
        }

        if (request.Date < job.StartDate)
        {
            throw new InvalidOperationException($"Attendance cannot be recorded before the job start date ({job.StartDate:yyyy-MM-dd}).");
        }

        if (job.EndDate != default && request.Date > job.EndDate)
        {
            throw new InvalidOperationException($"Attendance cannot be recorded after the job end date ({job.EndDate:yyyy-MM-dd}).");
        }

        var validAssignments = await _dbContext.WorkerAssignments
            .Where(w => w.JobId == jobId)
            .ToDictionaryAsync(w => w.Id);

        var existingRecords = await _dbContext.Attendances
            .Where(a => a.WorkerAssignment.JobId == jobId && a.Date == request.Date)
            .ToDictionaryAsync(a => a.WorkerAssignmentId);

        var updatedOrCreated = new List<Attendance>();

        foreach (var item in request.Items)
        {
            if (!Enum.IsDefined(typeof(Domain.Enums.AttendanceStatus), item.Status))
            {
                throw new InvalidOperationException($"Invalid attendance status '{item.Status}'.");
            }

            if (!validAssignments.TryGetValue(item.WorkerAssignmentId, out var assignment))
            {
                throw new InvalidOperationException($"Worker assignment '{item.WorkerAssignmentId}' does not belong to this job.");
            }

            if (existingRecords.TryGetValue(item.WorkerAssignmentId, out var existing))
            {
                existing.Status = item.Status;
                existing.Notes = item.Notes;
                existing.CheckIn = item.CheckIn;
                existing.CheckOut = item.CheckOut;
                existing.TotalHours = item.TotalHours ?? (item.Status == Domain.Enums.AttendanceStatus.HalfDay ? 4m : item.Status == Domain.Enums.AttendanceStatus.Present ? 8m : 0m);
                updatedOrCreated.Add(existing);
            }
            else
            {
                var newRecord = new Attendance
                {
                    WorkerAssignmentId = item.WorkerAssignmentId,
                    Date = request.Date,
                    Status = item.Status,
                    Notes = item.Notes,
                    CheckIn = item.CheckIn,
                    CheckOut = item.CheckOut,
                    TotalHours = item.TotalHours ?? (item.Status == Domain.Enums.AttendanceStatus.HalfDay ? 4m : item.Status == Domain.Enums.AttendanceStatus.Present ? 8m : 0m)
                };
                _dbContext.Attendances.Add(newRecord);
                updatedOrCreated.Add(newRecord);
            }
        }

        await _dbContext.SaveChangesAsync();

        // Reload updated items with navigation properties
        var attendanceIds = updatedOrCreated.Select(a => a.Id).ToList();
        var reloaded = await _dbContext.Attendances
            .AsNoTracking()
            .Include(a => a.WorkerAssignment)
                .ThenInclude(w => w.WorkerProfile)
            .Where(a => attendanceIds.Contains(a.Id))
            .ToListAsync();

        foreach (var att in reloaded)
        {
            if (att.WorkerAssignment?.WorkerProfile != null)
            {
                var workerUserId = att.WorkerAssignment.WorkerProfile.UserId.ToString();
                var jobTitle = job.Title;
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        workerUserId,
                        "Attendance Updated",
                        $"Your attendance for '{jobTitle}' on {att.Date:yyyy-MM-dd} was marked {att.Status}.",
                        NotificationType.Job,
                        att.Id
                    );
                }
                catch { }
            }
        }

        return reloaded.Select(ToResponse).ToList();
    }

    public async Task<FarmerAttendanceResponse> UpdateAttendanceRecordAsync(Guid userId, Guid attendanceId, UpdateAttendanceRecordRequest request)
    {
        var farmerProfile = await GetFarmerProfileAsync(userId);

        var attendance = await _dbContext.Attendances
            .Include(a => a.WorkerAssignment)
                .ThenInclude(w => w.Job)
            .Include(a => a.WorkerAssignment)
                .ThenInclude(w => w.WorkerProfile)
            .SingleOrDefaultAsync(a => a.Id == attendanceId && a.WorkerAssignment.Job.FarmerProfileId == farmerProfile.Id);

        if (attendance is null)
        {
            throw new JobNotFoundException("Attendance record not found.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (attendance.Date > today)
        {
            throw new InvalidOperationException("Attendance cannot be recorded for a future date.");
        }

        if (attendance.Date < attendance.WorkerAssignment.Job.StartDate)
        {
            throw new InvalidOperationException("Attendance cannot be recorded before the job start date.");
        }

        if (attendance.WorkerAssignment.Job.EndDate != default && attendance.Date > attendance.WorkerAssignment.Job.EndDate)
        {
            throw new InvalidOperationException("Attendance cannot be recorded after the job end date.");
        }

        attendance.Status = request.Status;
        attendance.Notes = request.Notes;
        attendance.CheckIn = request.CheckIn;
        attendance.CheckOut = request.CheckOut;
        if (request.TotalHours.HasValue)
        {
            attendance.TotalHours = request.TotalHours.Value;
        }

        await _dbContext.SaveChangesAsync();

        if (attendance.WorkerAssignment?.WorkerProfile != null)
        {
            var workerUserId = attendance.WorkerAssignment.WorkerProfile.UserId.ToString();
            var jobTitle = attendance.WorkerAssignment.Job?.Title ?? "Job";
            try
            {
                await _notificationService.CreateNotificationAsync(
                    workerUserId,
                    "Attendance Updated",
                    $"Your attendance for '{jobTitle}' on {attendance.Date:yyyy-MM-dd} was marked {attendance.Status}.",
                    NotificationType.Job,
                    attendance.Id
                );
            }
            catch { }
        }
        return ToResponse(attendance);
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

    private async Task VerifyJobOwnershipAsync(Guid farmerProfileId, Guid jobId)
    {
        var jobExists = await _dbContext.Jobs
            .AsNoTracking()
            .AnyAsync(j => j.Id == jobId && j.FarmerProfileId == farmerProfileId);

        if (!jobExists)
        {
            throw new JobNotFoundException("Job not found.");
        }
    }

    private static FarmerAttendanceResponse ToResponse(Attendance a)
    {
        return new FarmerAttendanceResponse(
            AttendanceId: a.Id,
            WorkerAssignmentId: a.WorkerAssignmentId,
            WorkerProfileId: a.WorkerAssignment?.WorkerProfileId ?? Guid.Empty,
            WorkerName: a.WorkerAssignment?.WorkerProfile?.FullName ?? "Worker",
            WorkerPhone: a.WorkerAssignment?.WorkerProfile?.Phone ?? string.Empty,
            Date: a.Date,
            Status: a.Status,
            Notes: a.Notes,
            CheckIn: a.CheckIn,
            CheckOut: a.CheckOut,
            TotalHours: a.TotalHours
        );
    }
}
