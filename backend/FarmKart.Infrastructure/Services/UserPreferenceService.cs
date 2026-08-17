using FarmKart.Application.Abstractions.UserPreference;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class UserPreferenceService : IUserPreferenceService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserPreferenceService(
        FarmKartDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<UserPreferenceResponse> GetUserPreferenceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pref = await _dbContext.UserPreferences
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (pref == null)
        {
            pref = new UserPreference
            {
                UserId = userId,
                Theme = "light",
                Language = "en",
                EmailAlerts = true,
                SmsAlerts = false,
                CompactView = false
            };
            _dbContext.UserPreferences.Add(pref);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapToResponse(pref);
    }

    public async Task<UserPreferenceResponse> UpdateUserPreferenceAsync(Guid userId, UpdateUserPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        var validThemes = new[] { "light", "dark", "system" };
        var validLanguages = new[] { "en", "hi", "gu" };

        var theme = string.IsNullOrWhiteSpace(request.Theme) || !Array.Exists(validThemes, t => t.Equals(request.Theme, StringComparison.OrdinalIgnoreCase))
            ? "light"
            : request.Theme.ToLowerInvariant();

        var lang = string.IsNullOrWhiteSpace(request.Language) || !Array.Exists(validLanguages, l => l.Equals(request.Language, StringComparison.OrdinalIgnoreCase))
            ? "en"
            : request.Language.ToLowerInvariant();

        var pref = await _dbContext.UserPreferences
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (pref == null)
        {
            pref = new UserPreference
            {
                UserId = userId
            };
            _dbContext.UserPreferences.Add(pref);
        }

        pref.Theme = theme;
        pref.Language = lang;
        pref.EmailAlerts = request.EmailAlerts;
        pref.SmsAlerts = request.SmsAlerts;
        pref.CompactView = request.CompactView;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(pref);
    }

    public async Task<AccountSettingsResponse> GetAccountSettingsAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        string fullName = string.Empty;
        string phone = user.PhoneNumber ?? string.Empty;

        if (role == Roles.Farmer)
        {
            var profile = await _dbContext.FarmerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            fullName = profile?.FullName ?? string.Empty;
            if (!string.IsNullOrEmpty(profile?.Phone)) phone = profile.Phone;
        }
        else if (role == Roles.Worker)
        {
            var profile = await _dbContext.WorkerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            fullName = profile?.FullName ?? string.Empty;
            if (!string.IsNullOrEmpty(profile?.Phone)) phone = profile.Phone;
        }
        else if (role == Roles.Customer)
        {
            var profile = await _dbContext.CustomerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            fullName = profile?.FullName ?? string.Empty;
            if (!string.IsNullOrEmpty(profile?.Phone)) phone = profile.Phone;
        }

        return new AccountSettingsResponse(
            UserId: user.Id,
            FullName: fullName,
            Email: user.Email!,
            Role: role,
            Phone: phone
        );
    }

    public async Task<AccountSettingsResponse> UpdateAccountProfileAsync(Guid userId, string role, UpdateAccountProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ArgumentException("Full name cannot be empty.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.PhoneNumber = request.Phone?.Trim();
        await _userManager.UpdateAsync(user);

        string updatedName = request.FullName.Trim();
        string updatedPhone = request.Phone?.Trim() ?? string.Empty;

        if (role == Roles.Farmer)
        {
            var profile = await _dbContext.FarmerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (profile != null)
            {
                profile.FullName = updatedName;
                profile.Phone = updatedPhone;
            }
        }
        else if (role == Roles.Worker)
        {
            var profile = await _dbContext.WorkerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (profile != null)
            {
                profile.FullName = updatedName;
                profile.Phone = updatedPhone;
            }
        }
        else if (role == Roles.Customer)
        {
            var profile = await _dbContext.CustomerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (profile != null)
            {
                profile.FullName = updatedName;
                profile.Phone = updatedPhone;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AccountSettingsResponse(
            UserId: user.Id,
            FullName: updatedName,
            Email: user.Email!,
            Role: role,
            Phone: updatedPhone
        );
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new ArgumentException("Current password is required.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new ArgumentException("New password is required.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            throw new ArgumentException("New password and confirm password do not match.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", System.Linq.Enumerable.Select(result.Errors, e => e.Description));
            throw new InvalidOperationException(errors);
        }
    }

    private static UserPreferenceResponse MapToResponse(UserPreference pref)
    {
        return new UserPreferenceResponse(
            Theme: pref.Theme,
            Language: pref.Language,
            EmailAlerts: pref.EmailAlerts,
            SmsAlerts: pref.SmsAlerts,
            CompactView: pref.CompactView
        );
    }
}
