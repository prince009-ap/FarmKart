using System;

namespace FarmKart.Application.DTOs;

/// <summary>
/// Login Response DTO containing safe user details.
/// Note: The JWT access token is stored inside an HttpOnly cookie and is not returned in the JSON payload.
/// </summary>
public record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string? ProfileImageUrl,
    DateTime ExpiresAt,
    string Message
);
