using FarmKart.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record MarkAttendanceItemRequest(
    Guid WorkerAssignmentId,
    AttendanceStatus Status,
    string? Notes = null,
    TimeOnly? CheckIn = null,
    TimeOnly? CheckOut = null,
    decimal? TotalHours = null
);

public record SaveJobAttendanceRequest(
    DateOnly Date,
    IReadOnlyList<MarkAttendanceItemRequest> Items
);

public record UpdateAttendanceRecordRequest(
    AttendanceStatus Status,
    string? Notes = null,
    TimeOnly? CheckIn = null,
    TimeOnly? CheckOut = null,
    decimal? TotalHours = null
);

public record FarmerAttendanceResponse(
    Guid AttendanceId,
    Guid WorkerAssignmentId,
    Guid WorkerProfileId,
    string WorkerName,
    string WorkerPhone,
    DateOnly Date,
    AttendanceStatus Status,
    string? Notes,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    decimal TotalHours
);
