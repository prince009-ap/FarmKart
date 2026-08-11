using System;

namespace FarmKart.Application.DTOs;

public record WorkerRegistrationResponse(
    Guid UserId,
    string Role,
    string FullName,
    string Email,
    string Message
);
