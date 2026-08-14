using System.ComponentModel.DataAnnotations;
using FarmKart.Domain.Enums;

namespace FarmKart.Application.DTOs;

public sealed record CropImageResponse(
    Guid Id,
    Guid CropId,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder,
    DateTime CreatedAtUtc
);

public sealed record CropResponse(
    Guid Id,
    Guid FarmerProfileId,
    string FarmerName,
    string CropName,
    string CropType,
    string? Variety,
    decimal Area,
    string AreaUnit,
    DateOnly? SowingDate,
    DateOnly? ExpectedHarvestDate,
    DateOnly? ActualHarvestDate,
    decimal Quantity,
    string Unit,
    string? QualityGrade,
    string? Description,
    string Status,
    string? PrimaryImageUrl,
    IReadOnlyList<CropImageResponse> Images,
    decimal AvailableQuantityKg,
    string AvailableQuantityFormatted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed record CreateCropRequest(
    [Required(ErrorMessage = "Crop name is required.")]
    [StringLength(120, ErrorMessage = "Crop name cannot exceed 120 characters.")]
    string CropName,

    [Required(ErrorMessage = "Crop type is required.")]
    [StringLength(120, ErrorMessage = "Crop type cannot exceed 120 characters.")]
    string CropType,

    [StringLength(120, ErrorMessage = "Variety cannot exceed 120 characters.")]
    string? Variety,

    [Range(0.01, 100000.0, ErrorMessage = "Cultivated area must be greater than zero.")]
    decimal Area,

    string? AreaUnit,

    DateOnly? SowingDate,

    DateOnly? ExpectedHarvestDate,

    DateOnly? ActualHarvestDate,

    string? Status,

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    string? Description
);

public sealed record UpdateCropRequest(
    [Required(ErrorMessage = "Crop name is required.")]
    [StringLength(120, ErrorMessage = "Crop name cannot exceed 120 characters.")]
    string CropName,

    [Required(ErrorMessage = "Crop type is required.")]
    [StringLength(120, ErrorMessage = "Crop type cannot exceed 120 characters.")]
    string CropType,

    [StringLength(120, ErrorMessage = "Variety cannot exceed 120 characters.")]
    string? Variety,

    [Range(0.01, 100000.0, ErrorMessage = "Cultivated area must be greater than zero.")]
    decimal Area,

    string? AreaUnit,

    DateOnly? SowingDate,

    DateOnly? ExpectedHarvestDate,

    DateOnly? ActualHarvestDate,

    string? Status,

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    string? Description
);
