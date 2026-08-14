using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record WorkerWorkHistoryItemResponse(
    Guid AssignmentId,
    Guid JobId,
    string JobTitle,
    string WorkCategory,
    string FarmerName,
    string Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal DailyWage,
    decimal DaysWorked,
    int PresentCount,
    int HalfDayCount,
    decimal TotalEarned,
    int? Rating,
    string? ReviewComment,
    string Status,
    DateTime CompletedAtUtc
);

public record WorkerWorkHistorySummaryResponse(
    int TotalCompletedJobs,
    decimal TotalWorkDays,
    decimal TotalEarnings,
    IReadOnlyList<WorkerWorkHistoryItemResponse> HistoryItems
);
