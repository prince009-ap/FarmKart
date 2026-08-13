using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public record CreateFarmerJobRequest(
    [Required, StringLength(150)] string Title,
    [Required, StringLength(2000)] string Description,
    [Required, StringLength(100)] string WorkCategory,
    [Range(1, int.MaxValue)] int WorkersRequired,
    [Range(0, int.MaxValue)] int RequiredExperience,
    [Range(0, double.MaxValue)] decimal WagePerDay,
    DateOnly StartDate,
    DateOnly EndDate,
    [Required, StringLength(100)] string WorkingHours,
    [Required, StringLength(250)] string FarmLocation,
    string? CropType = null,
    [Range(0, double.MaxValue)] decimal? FarmSize = null,
    bool FoodProvided = false,
    bool AccommodationProvided = false,
    bool IsUrgent = false);

public record UpdateFarmerJobRequest(
    [Required, StringLength(150)] string Title,
    [Required, StringLength(2000)] string Description,
    [Required, StringLength(100)] string WorkCategory,
    [Range(1, int.MaxValue)] int WorkersRequired,
    [Range(0, int.MaxValue)] int RequiredExperience,
    [Range(0, double.MaxValue)] decimal WagePerDay,
    DateOnly StartDate,
    DateOnly EndDate,
    [Required, StringLength(100)] string WorkingHours,
    [Required, StringLength(250)] string FarmLocation,
    string? CropType = null,
    [Range(0, double.MaxValue)] decimal? FarmSize = null,
    bool FoodProvided = false,
    bool AccommodationProvided = false,
    bool IsUrgent = false);
