using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public record WorkerRegisterRequest(
    [Required(ErrorMessage = "FullName is required.")]
    string FullName,

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    string Email,

    [Required(ErrorMessage = "Password is required.")]
    string Password,

    [Required(ErrorMessage = "Phone is required.")]
    string Phone,

    string? ProfileImageUrl,

    [Required(ErrorMessage = "Address is required.")]
    string Address,

    [Required(ErrorMessage = "ExperienceYears is required.")]
    [Range(0, 100, ErrorMessage = "ExperienceYears must be non-negative.")]
    int ExperienceYears,

    [Required(ErrorMessage = "ExpectedDailyWage is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "ExpectedDailyWage must not be negative.")]
    decimal ExpectedDailyWage
);
