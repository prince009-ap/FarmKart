using FarmKart.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record FarmerJobApplicationResponse(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid ApplicantWorkerId,
    string ApplicantName,
    string ApplicantPhone,
    int ApplicantExperienceYears,
    IReadOnlyList<string> ApplicantSkills,
    ApplicationStatus Status,
    DateTime AppliedAtUtc,
    string? Message
);
