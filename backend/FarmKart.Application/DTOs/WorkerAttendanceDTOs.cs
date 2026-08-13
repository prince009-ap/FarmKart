using FarmKart.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record WorkerAttendanceRecordResponse(
    Guid AttendanceId,
    Guid WorkerAssignmentId,
    Guid JobId,
    string JobTitle,
    string FarmerName,
    DateOnly Date,
    AttendanceStatus Status,
    string? Notes,
    decimal TotalHours
);

public record WorkerAttendanceSummaryResponse(
    int TotalDays,
    int PresentDays,
    int AbsentDays,
    int HalfDays,
    int LeaveDays,
    decimal AttendancePercentage,
    IReadOnlyList<WorkerAttendanceRecordResponse> History
);
