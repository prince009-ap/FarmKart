using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record WorkerEarningsItemResponse(
    Guid AssignmentId,
    Guid JobId,
    string JobTitle,
    string FarmerName,
    string WorkCategory,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal DaysWorked,
    decimal DailyWage,
    decimal TotalEarned,
    string Status,
    DateTime AssignedAtUtc
);

public record WorkerEarningsSummaryResponse(
    decimal TotalEarnings,
    int CompletedJobsCount,
    decimal ThisMonthEarnings,
    decimal AllTimeEarnings,
    IReadOnlyList<WorkerEarningsItemResponse> EarningsHistory
);
