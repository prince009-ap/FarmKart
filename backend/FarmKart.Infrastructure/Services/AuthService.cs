using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.ValueObjects;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FarmKartDbContext _dbContext;

    public AuthService(UserManager<ApplicationUser> userManager, FarmKartDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
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
                    AddressLine = request.Address,
                    City = request.City,
                    State = request.State,
                    Pincode = request.Pincode,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
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
                    AddressLine = request.Address,
                    City = request.City,
                    State = request.State,
                    Pincode = request.Pincode,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
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
                    AddressLine = request.Address,
                    City = request.City,
                    State = request.State,
                    Pincode = request.Pincode,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
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
}
