using System;

namespace FarmKart.Application.DTOs;

public record CustomerProfileResponse(
    Guid CustomerProfileId,
    Guid UserId,
    string FullName,
    string Email,
    string Phone,
    string Address,
    string? ProfileImageUrl,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record UpdateCustomerProfileRequest(
    string FullName,
    string Phone,
    string Address
);
