using FarmKart.Domain.Enums;
using System;

namespace FarmKart.Application.DTOs;

public record WorkerJobApplicationResponse(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    string WorkCategory,
    decimal WagePerDay,
    DateOnly StartDate,
    DateOnly EndDate,
    string FarmLocation,
    ApplicationStatus Status,
    DateTime AppliedAtUtc,
    string? Message
);
