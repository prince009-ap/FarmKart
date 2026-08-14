using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record WorkerProfileResponse(
    Guid UserId,
    string FullName,
    string Email,
    string Phone,
    string Address,
    string? ProfileImageUrl,
    int ExperienceYears,
    decimal ExpectedDailyWage,
    bool IsAvailable,
    DateOnly? AvailableFrom,
    string? AvailabilityNotes,
    string? ExperienceDescription = null,
    IReadOnlyList<string>? Skills = null
);
