using System;

namespace FarmKart.Application.DTOs;

public record AuthUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role
);
