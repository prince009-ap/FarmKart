using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public record CustomerRegisterRequest(
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
    string Address
);
