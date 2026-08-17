using System;

namespace FarmKart.Application.DTOs;

public record LoginResult(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string? ProfileImageUrl,
    string Token,
    DateTime ExpiresAt,
    string Message
);
