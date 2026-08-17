using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Abstractions.Profile;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class CustomerProfileService : ICustomerProfileService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IProfileImageService _profileImageService;

    public CustomerProfileService(
        FarmKartDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IProfileImageService profileImageService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _profileImageService = profileImageService;
    }

    public async Task<CustomerProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.CustomerProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            throw new InvalidOperationException($"Customer profile not found for user '{userId}'.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        var email = user?.Email ?? string.Empty;

        return MapToResponse(profile, email);
    }

    public async Task<CustomerProfileResponse> UpdateProfileAsync(Guid userId, UpdateCustomerProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ArgumentException("Full name cannot be empty.");
        }

        var profile = await _dbContext.CustomerProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            throw new InvalidOperationException($"Customer profile not found for user '{userId}'.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null)
        {
            user.PhoneNumber = request.Phone?.Trim();
            await _userManager.UpdateAsync(user);
        }

        profile.FullName = request.FullName.Trim();
        profile.Phone = request.Phone?.Trim() ?? string.Empty;
        if (profile.AddressInfo != null)
        {
            profile.AddressInfo.AddressLine = request.Address?.Trim() ?? string.Empty;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(profile, user?.Email ?? string.Empty);
    }

    public async Task<CustomerProfileResponse> UploadProfileImageAsync(
        Guid userId,
        Stream stream,
        string fileName,
        string contentType,
        long fileLength,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.CustomerProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            throw new InvalidOperationException($"Customer profile not found for user '{userId}'.");
        }

        var newImageUrl = await _profileImageService.UploadProfileImageAsync(
            userId, stream, fileName, contentType, fileLength, profile.ProfileImageUrl, cancellationToken);

        profile.ProfileImageUrl = newImageUrl;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        return MapToResponse(profile, user?.Email ?? string.Empty);
    }

    public async Task<CustomerProfileResponse> RemoveProfileImageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.CustomerProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            throw new InvalidOperationException($"Customer profile not found for user '{userId}'.");
        }

        if (!string.IsNullOrEmpty(profile.ProfileImageUrl))
        {
            _profileImageService.DeleteProfileImage(profile.ProfileImageUrl);
            profile.ProfileImageUrl = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        return MapToResponse(profile, user?.Email ?? string.Empty);
    }

    private static CustomerProfileResponse MapToResponse(CustomerProfile profile, string email)
    {
        return new CustomerProfileResponse(
            CustomerProfileId: profile.Id,
            UserId: profile.UserId,
            FullName: profile.FullName,
            Email: email,
            Phone: profile.Phone,
            Address: profile.AddressInfo?.AddressLine ?? string.Empty,
            ProfileImageUrl: profile.ProfileImageUrl,
            CreatedAtUtc: profile.CreatedAtUtc,
            UpdatedAtUtc: profile.UpdatedAtUtc
        );
    }
}
