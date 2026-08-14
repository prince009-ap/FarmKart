using FarmKart.Application.Abstractions.Worker;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Domain.ValueObjects;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public class WorkerProfileService : IWorkerProfileService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkerProfileService(FarmKartDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<WorkerProfileResponse> GetProfileAsync(Guid userId)
    {
        var profile = await _dbContext.WorkerProfiles
            .Include(p => p.WorkerSkills)
                .ThenInclude(ws => ws.Skill)
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            throw new ProfileNotFoundException();
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        var email = user?.Email ?? string.Empty;

        var skills = profile.WorkerSkills
            .Select(ws => ws.Skill.Name)
            .OrderBy(name => name)
            .ToList();

        return new WorkerProfileResponse(
            UserId: profile.UserId,
            FullName: profile.FullName,
            Email: email,
            Phone: profile.Phone,
            Address: profile.AddressInfo.AddressLine,
            ProfileImageUrl: profile.ProfileImageUrl,
            ExperienceYears: profile.ExperienceYears,
            ExpectedDailyWage: profile.ExpectedDailyWage,
            IsAvailable: profile.IsAvailable,
            AvailableFrom: profile.AvailableFrom,
            AvailabilityNotes: profile.AvailabilityNotes,
            ExperienceDescription: profile.ExperienceDescription,
            Skills: skills
        );
    }

    public async Task<WorkerProfileResponse> UpdateProfileAsync(Guid userId, WorkerProfileUpdateRequest request)
    {
        if (request.ExperienceYears < 0)
        {
            throw new ArgumentException("ExperienceYears must not be negative.");
        }

        if (request.ExpectedDailyWage < 0)
        {
            throw new ArgumentException("ExpectedDailyWage must not be negative.");
        }

        // Validate phone format
        if (string.IsNullOrWhiteSpace(request.Phone) || !Regex.IsMatch(request.Phone.Trim(), @"^\+?[0-9\s\-]{7,20}$"))
        {
            throw new ArgumentException("Invalid phone number format.");
        }

        var profile = await _dbContext.WorkerProfiles
            .Include(p => p.WorkerSkills)
                .ThenInclude(ws => ws.Skill)
            .SingleOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            throw new ProfileNotFoundException();
        }

        // Process skills if provided
        if (request.Skills != null)
        {
            // Validate empty/whitespace skills
            foreach (var s in request.Skills)
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    throw new ArgumentException("Skill name cannot be empty.");
                }
            }

            // Deduplicate and trim skill names
            var cleanedSkillNames = request.Skills
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Remove existing worker skills for this profile
            var existingWorkerSkills = await _dbContext.WorkerSkills
                .Where(ws => ws.WorkerProfileId == profile.Id)
                .ToListAsync();

            if (existingWorkerSkills.Count > 0)
            {
                _dbContext.WorkerSkills.RemoveRange(existingWorkerSkills);
                await _dbContext.SaveChangesAsync();
            }

            // Add new worker skills
            foreach (var skillName in cleanedSkillNames)
            {
                var lowerName = skillName.ToLower();
                var dbSkill = await _dbContext.Skills
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == lowerName);

                if (dbSkill == null)
                {
                    dbSkill = new Skill { Name = skillName };
                    _dbContext.Skills.Add(dbSkill);
                    await _dbContext.SaveChangesAsync();
                }

                _dbContext.WorkerSkills.Add(new WorkerSkill
                {
                    WorkerProfileId = profile.Id,
                    SkillId = dbSkill.Id
                });
            }
        }

        profile.FullName = request.FullName.Trim();
        profile.Phone = request.Phone.Trim();
        profile.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? null : request.ProfileImageUrl.Trim();
        profile.AddressInfo = new AddressInfo
        {
            AddressLine = request.Address.Trim()
        };
        profile.ExperienceYears = request.ExperienceYears;
        profile.ExperienceDescription = string.IsNullOrWhiteSpace(request.ExperienceDescription) ? null : request.ExperienceDescription.Trim();
        profile.ExpectedDailyWage = request.ExpectedDailyWage;
        profile.IsAvailable = request.IsAvailable;
        profile.AvailableFrom = request.AvailableFrom;
        profile.AvailabilityNotes = string.IsNullOrWhiteSpace(request.AvailabilityNotes) ? null : request.AvailabilityNotes.Trim();

        await _dbContext.SaveChangesAsync();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        var email = user?.Email ?? string.Empty;

        var updatedSkills = await _dbContext.WorkerSkills
            .Where(ws => ws.WorkerProfileId == profile.Id)
            .Select(ws => ws.Skill.Name)
            .OrderBy(name => name)
            .ToListAsync();

        return new WorkerProfileResponse(
            UserId: profile.UserId,
            FullName: profile.FullName,
            Email: email,
            Phone: profile.Phone,
            Address: profile.AddressInfo.AddressLine,
            ProfileImageUrl: profile.ProfileImageUrl,
            ExperienceYears: profile.ExperienceYears,
            ExpectedDailyWage: profile.ExpectedDailyWage,
            IsAvailable: profile.IsAvailable,
            AvailableFrom: profile.AvailableFrom,
            AvailabilityNotes: profile.AvailabilityNotes,
            ExperienceDescription: profile.ExperienceDescription,
            Skills: updatedSkills
        );
    }
}
