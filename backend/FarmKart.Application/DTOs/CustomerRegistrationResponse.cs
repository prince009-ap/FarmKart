using System;

namespace FarmKart.Application.DTOs;

public record CustomerRegistrationResponse(
    Guid UserId,
    string Role,
    string FullName,
    string Email,
    string Message
);
