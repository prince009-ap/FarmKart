using FarmKart.Domain.Enums;
using System;

namespace FarmKart.Application.DTOs;

public record FarmerProfileResponse(
    Guid UserId,
    string FullName,
    string Email,
    string Phone,
    string Address,
    string? FarmName,
    decimal? FarmSize,
    FarmSizeUnit? FarmSizeUnit,
    string? FarmLocation
);
