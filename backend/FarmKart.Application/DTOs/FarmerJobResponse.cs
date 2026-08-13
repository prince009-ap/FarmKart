using FarmKart.Domain.Enums;

namespace FarmKart.Application.DTOs;

public record FarmerJobResponse(
    Guid Id,
    string Title,
    string Description,
    string WorkCategory,
    string? CropType,
    int WorkersRequired,
    int RequiredExperience,
    decimal WagePerDay,
    DateOnly StartDate,
    DateOnly EndDate,
    string WorkingHours,
    string FarmLocation,
    decimal? FarmSize,
    bool FoodProvided,
    bool AccommodationProvided,
    bool IsUrgent,
    JobStatus Status,
    DateTime CreatedAtUtc);
