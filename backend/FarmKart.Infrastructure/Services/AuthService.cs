using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.ValueObjects;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Application.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FarmKartDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager, 
        FarmKartDbContext dbContext,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<FarmerRegistrationResponse> RegisterFarmerAsync(FarmerRegisterRequest request)
    {
        // 1. Check if email is already registered
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new DuplicateEmailException(request.Email);
        }

        // 2. Execute registration within a database transaction to ensure consistency
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.Phone
            };

            // Create Identity User
            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new RegistrationFailedException(errors);
            }

            // Assign Farmer Role
            var roleResult = await _userManager.AddToRoleAsync(user, Roles.Farmer);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                throw new RegistrationFailedException($"Role assignment failed: {errors}");
            }

            // Create FarmerProfile
            var farmerProfile = new FarmerProfile
            {
                UserId = user.Id,
                FullName = request.FullName,
                Phone = request.Phone,
                ProfileImageUrl = request.ProfileImageUrl,
                FarmName = request.FarmName,
                FarmSize = request.FarmSize,
                FarmLocation = request.FarmLocation,
                AddressInfo = new AddressInfo
                {
                    AddressLine = request.Address
                }
            };

            _dbContext.FarmerProfiles.Add(farmerProfile);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            return new FarmerRegistrationResponse(
                UserId: user.Id,
                Role: Roles.Farmer,
                FullName: request.FullName,
                Email: request.Email,
                Message: "Farmer registered successfully."
            );
        }
        catch (DuplicateEmailException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (RegistrationFailedException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new RegistrationFailedException($"Registration failed: {ex.Message}");
        }
    }

    public async Task<WorkerRegistrationResponse> RegisterWorkerAsync(WorkerRegisterRequest request)
    {
        // 1. Check if email is already registered
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new DuplicateEmailException(request.Email);
        }

        // 2. Execute registration within a database transaction
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.Phone
            };

            // Create Identity User
            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new RegistrationFailedException(errors);
            }

            // Assign Worker Role
            var roleResult = await _userManager.AddToRoleAsync(user, Roles.Worker);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                throw new RegistrationFailedException($"Role assignment failed: {errors}");
            }

            // Create WorkerProfile
            var workerProfile = new WorkerProfile
            {
                UserId = user.Id,
                FullName = request.FullName,
                Phone = request.Phone,
                ProfileImageUrl = request.ProfileImageUrl,
                ExperienceYears = request.ExperienceYears,
                ExpectedDailyWage = request.ExpectedDailyWage,
                IsAvailable = true,
                AddressInfo = new AddressInfo
                {
                    AddressLine = request.Address
                }
            };

            _dbContext.WorkerProfiles.Add(workerProfile);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            return new WorkerRegistrationResponse(
                UserId: user.Id,
                Role: Roles.Worker,
                FullName: request.FullName,
                Email: request.Email,
                Message: "Worker registered successfully."
            );
        }
        catch (DuplicateEmailException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (RegistrationFailedException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new RegistrationFailedException($"Registration failed: {ex.Message}");
        }
    }

    public async Task<CustomerRegistrationResponse> RegisterCustomerAsync(CustomerRegisterRequest request)
    {
        // 1. Check if email is already registered
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new DuplicateEmailException(request.Email);
        }

        // 2. Execute registration within a database transaction
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.Phone
            };

            // Create Identity User
            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new RegistrationFailedException(errors);
            }

            // Assign Customer Role
            var roleResult = await _userManager.AddToRoleAsync(user, Roles.Customer);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                throw new RegistrationFailedException($"Role assignment failed: {errors}");
            }

            // Create CustomerProfile
            var customerProfile = new CustomerProfile
            {
                UserId = user.Id,
                FullName = request.FullName,
                Phone = request.Phone,
                ProfileImageUrl = request.ProfileImageUrl,
                AddressInfo = new AddressInfo
                {
                    AddressLine = request.Address
                }
            };

            _dbContext.CustomerProfiles.Add(customerProfile);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            return new CustomerRegistrationResponse(
                UserId: user.Id,
                Role: Roles.Customer,
                FullName: request.FullName,
                Email: request.Email,
                Message: "Customer registered successfully."
            );
        }
        catch (DuplicateEmailException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (RegistrationFailedException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new RegistrationFailedException($"Registration failed: {ex.Message}");
        }
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        // 1. Find user by email
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new InvalidCredentialsException();
        }

        // 2. Verify password
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            throw new InvalidCredentialsException();
        }

        // 3. Resolve role
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault();
        if (string.IsNullOrEmpty(role))
        {
            throw new InvalidCredentialsException();
        }

        // 4. Retrieve profile info for the full name
        string fullName = string.Empty;
        if (role == Roles.Farmer)
        {
            var profile = await _dbContext.FarmerProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id);
            fullName = profile?.FullName ?? string.Empty;
        }
        else if (role == Roles.Worker)
        {
            var profile = await _dbContext.WorkerProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id);
            fullName = profile?.FullName ?? string.Empty;
        }
        else if (role == Roles.Customer)
        {
            var profile = await _dbContext.CustomerProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id);
            fullName = profile?.FullName ?? string.Empty;
        }
        else
        {
            throw new InvalidCredentialsException();
        }

        var token = _jwtTokenService.GenerateToken(user.Id, user.Email!, role);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        return new LoginResult(
            UserId: user.Id,
            Email: user.Email!,
            FullName: fullName,
            Role: role,
            Token: token,
            ExpiresAt: expiresAt,
            Message: "Login successful."
        );
    }

    public async Task<AuthUserResponse> GetCurrentUserAsync(Guid userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidCredentialsException();
        }

        string fullName = string.Empty;
        if (role == Roles.Farmer)
        {
            var profile = await _dbContext.FarmerProfiles.SingleOrDefaultAsync(p => p.UserId == userId);
            fullName = profile?.FullName ?? string.Empty;
        }
        else if (role == Roles.Worker)
        {
            var profile = await _dbContext.WorkerProfiles.SingleOrDefaultAsync(p => p.UserId == userId);
            fullName = profile?.FullName ?? string.Empty;
        }
        else if (role == Roles.Customer)
        {
            var profile = await _dbContext.CustomerProfiles.SingleOrDefaultAsync(p => p.UserId == userId);
            fullName = profile?.FullName ?? string.Empty;
        }
        else
        {
            throw new InvalidCredentialsException();
        }

        return new AuthUserResponse(
            UserId: user.Id,
            Email: user.Email!,
            FullName: fullName,
            Role: role
        );
    }
}
