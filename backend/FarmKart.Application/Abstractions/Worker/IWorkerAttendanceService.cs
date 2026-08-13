using FarmKart.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Worker;

public interface IWorkerAttendanceService
{
    Task<WorkerAttendanceSummaryResponse> GetMyAttendanceHistoryAsync(Guid userId);
    Task<WorkerAttendanceSummaryResponse> GetAssignmentAttendanceAsync(Guid userId, Guid assignmentId);
}
