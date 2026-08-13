using FarmKart.Domain.Enums;
using System;

namespace FarmKart.Application.DTOs;

public record WorkerAssignmentResponse(
    Guid AssignmentId,
    Guid JobId,
    string JobTitle,
    string WorkCategory,
    decimal WagePerDay,
    string FarmerName,
    string FarmLocation,
    string WorkingHours,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateTime AssignedAtUtc,
    AssignmentStatus Status
);
