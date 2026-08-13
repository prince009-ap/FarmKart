using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public record ApplyJobRequest(
    [MaxLength(1000, ErrorMessage = "Message must not exceed 1000 characters.")]
    string? Message
);
