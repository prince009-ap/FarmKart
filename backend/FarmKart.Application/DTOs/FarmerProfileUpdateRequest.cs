using FarmKart.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public record FarmerProfileUpdateRequest(
    [Required(ErrorMessage = "FullName is required.")]
    [MaxLength(150, ErrorMessage = "FullName must not exceed 150 characters.")]
    string FullName,

    [Required(ErrorMessage = "Phone is required.")]
    [MaxLength(20, ErrorMessage = "Phone must not exceed 20 characters.")]
    string Phone,

    [Required(ErrorMessage = "Address is required.")]
    string Address,

    [MaxLength(150, ErrorMessage = "FarmName must not exceed 150 characters.")]
    string? FarmName,

    [Range(0, double.MaxValue, ErrorMessage = "FarmSize must not be negative.")]
    decimal? FarmSize,

    [EnumDataType(typeof(FarmSizeUnit), ErrorMessage = "FarmSizeUnit must be a valid value.")]
    FarmSizeUnit? FarmSizeUnit,

    [MaxLength(250, ErrorMessage = "FarmLocation must not exceed 250 characters.")]
    string? FarmLocation
);
