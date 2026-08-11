using System;

namespace FarmKart.Application.DTOs;

public record FarmerRegistrationResponse(
    Guid UserId,
    string Role,
    string FullName,
    string Email,
    string Message
);
