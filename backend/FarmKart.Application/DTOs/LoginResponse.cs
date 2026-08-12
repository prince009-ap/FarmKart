using System;

namespace FarmKart.Application.DTOs;

public record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Message
);
