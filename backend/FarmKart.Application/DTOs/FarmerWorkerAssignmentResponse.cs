using FarmKart.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record FarmerWorkerAssignmentResponse(
    Guid AssignmentId,
    Guid JobId,
    string JobTitle,
    Guid WorkerProfileId,
    string WorkerName,
    string WorkerPhone,
    int WorkerExperienceYears,
    IReadOnlyList<string> WorkerSkills,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateTime AssignedAtUtc,
    AssignmentStatus Status
);
