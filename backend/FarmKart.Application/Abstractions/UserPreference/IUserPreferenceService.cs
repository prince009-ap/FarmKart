using FarmKart.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.UserPreference;

public interface IUserPreferenceService
{
    Task<UserPreferenceResponse> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserPreferenceResponse> UpdateUserPreferenceAsync(Guid userId, UpdateUserPreferenceRequest request, CancellationToken cancellationToken = default);
    Task<AccountSettingsResponse> GetAccountSettingsAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<AccountSettingsResponse> UpdateAccountProfileAsync(Guid userId, string role, UpdateAccountProfileRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
