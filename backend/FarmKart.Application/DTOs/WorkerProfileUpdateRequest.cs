using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public record WorkerProfileUpdateRequest(
    [Required(ErrorMessage = "FullName is required.")]
    [MaxLength(150, ErrorMessage = "FullName must not exceed 150 characters.")]
    string FullName,

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(@"^\+?[0-9\s\-]{7,20}$", ErrorMessage = "Invalid phone number format.")]
    [MaxLength(20, ErrorMessage = "Phone must not exceed 20 characters.")]
    string Phone,

    [Required(ErrorMessage = "Address is required.")]
    string Address,

    [Range(0, 100, ErrorMessage = "ExperienceYears must be non-negative.")]
    int ExperienceYears,

    [Range(0, double.MaxValue, ErrorMessage = "ExpectedDailyWage must not be negative.")]
    decimal ExpectedDailyWage = 0,

    string? ProfileImageUrl = null,
    bool IsAvailable = true,
    DateOnly? AvailableFrom = null,
    string? AvailabilityNotes = null,

    [MaxLength(2000, ErrorMessage = "ExperienceDescription must not exceed 2000 characters.")]
    string? ExperienceDescription = null,

    List<string>? Skills = null
);
