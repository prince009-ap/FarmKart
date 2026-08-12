using System;

namespace FarmKart.Application.DTOs;

/// <summary>
/// Login Response DTO containing user information and the temporary JWT token.
/// Note: A subsequent phase will migrate JWT token storage to an HttpOnly cookie.
/// </summary>
public record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Token,
    DateTime ExpiresAt,
    string Message
);
