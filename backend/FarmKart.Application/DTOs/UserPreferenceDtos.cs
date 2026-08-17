using System;

namespace FarmKart.Application.DTOs;

public record UserPreferenceResponse(
    string Theme,
    string Language,
    bool EmailAlerts,
    bool SmsAlerts,
    bool CompactView
);

public record UpdateUserPreferenceRequest(
    string Theme,
    string Language,
    bool EmailAlerts,
    bool SmsAlerts,
    bool CompactView
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);

public record AccountSettingsResponse(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    string Phone
);

public record UpdateAccountProfileRequest(
    string FullName,
    string Phone
);
