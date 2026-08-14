using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public record WorkerPreferencesResponse(
    IReadOnlyList<string> PreferredWorkCategories,
    IReadOnlyList<string> PreferredLocations,
    decimal MinimumDailyWage,
    string? PreferredWorkingHours,
    string? FoodPreference,
    string? AccommodationPreference
);

public record WorkerPreferencesUpdateRequest(
    IReadOnlyList<string>? PreferredWorkCategories,
    IReadOnlyList<string>? PreferredLocations,
    [Range(0, 100000, ErrorMessage = "Minimum daily wage cannot be negative.")]
    decimal MinimumDailyWage,
    [MaxLength(100)]
    string? PreferredWorkingHours,
    [MaxLength(50)]
    string? FoodPreference,
    [MaxLength(50)]
    string? AccommodationPreference
);
