using System.ComponentModel.DataAnnotations;

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

    [Required(ErrorMessage = "City is required.")]
    string City,

    [Required(ErrorMessage = "State is required.")]
    string State,

    [Required(ErrorMessage = "Pincode is required.")]
    string Pincode,

    [Required(ErrorMessage = "FarmName is required.")]
    string FarmName,

    [Required(ErrorMessage = "FarmSize is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "FarmSize must not be negative.")]
    decimal FarmSize,

    string? FarmLocation,

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    decimal? Latitude,

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    decimal? Longitude
);
