using System.ComponentModel.DataAnnotations;
using FarmKart.Domain.Enums;

namespace FarmKart.Application.DTOs;

public record FarmerRegisterRequest(
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

    string? FarmName,

    [Required(ErrorMessage = "FarmSize is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "FarmSize must not be negative.")]
    decimal FarmSize,

    [Required(ErrorMessage = "FarmSizeUnit is required.")]
    [EnumDataType(typeof(FarmSizeUnit), ErrorMessage = "FarmSizeUnit must be a valid value.")]
    FarmSizeUnit FarmSizeUnit,

    string? FarmLocation
);
