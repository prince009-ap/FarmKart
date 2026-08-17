using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.Abstractions.Profile;
using FarmKart.Application.Abstractions.Worker;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class TestWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "FarmKart.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class ProfileImageTests
{
    private (ServiceProvider Provider, string DbName, string TempWebRoot) SetupServices()
    {
        var dbName = $"FarmKartDb_ProfileImageTest_{Guid.NewGuid()}";
        var tempWebRoot = Path.Combine(Path.GetTempPath(), $"FarmKartWebRoot_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempWebRoot);

        var services = new ServiceCollection();

        services.AddDbContext<FarmKartDbContext>(options =>
            options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True"));

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FarmKartDbContext>();

        var testEnv = new TestWebHostEnvironment
        {
            WebRootPath = tempWebRoot,
            ContentRootPath = tempWebRoot
        };

        services.AddSingleton<IWebHostEnvironment>(testEnv);
        services.AddScoped<IProfileImageService, ProfileImageService>();
        services.AddScoped<ICustomerProfileService, CustomerProfileService>();
        services.AddScoped<IFarmerProfileService, FarmerProfileService>();
        services.AddScoped<IWorkerProfileService, WorkerProfileService>();
        services.AddLogging();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            db.Database.EnsureCreated();
        }

        return (provider, dbName, tempWebRoot);
    }

    private void Cleanup(ServiceProvider provider, string dbName, string tempWebRoot)
    {
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            db.Database.EnsureDeleted();
        }
        if (Directory.Exists(tempWebRoot))
        {
            try { Directory.Delete(tempWebRoot, true); } catch { }
        }
    }

    [Fact]
    public async Task CustomerProfile_GetAndUpdate_Succeeds()
    {
        var (provider, dbName, webRoot) = SetupServices();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();

            var user = new ApplicationUser { UserName = "cust1@test.com", Email = "cust1@test.com", PhoneNumber = "1234567890" };
            await userManager.CreateAsync(user);

            var customerProfile = new CustomerProfile
            {
                UserId = user.Id,
                FullName = "Original Name",
                Phone = "1234567890",
                AddressInfo = new Domain.ValueObjects.AddressInfo { AddressLine = "123 Street" }
            };
            db.CustomerProfiles.Add(customerProfile);
            await db.SaveChangesAsync();

            // Get profile
            var profile = await customerService.GetProfileAsync(user.Id);
            Assert.Equal("Original Name", profile.FullName);
            Assert.Equal("cust1@test.com", profile.Email);

            // Update profile
            var updated = await customerService.UpdateProfileAsync(user.Id, new UpdateCustomerProfileRequest("New Customer Name", "9876543210", "456 New Ave"));
            Assert.Equal("New Customer Name", updated.FullName);
            Assert.Equal("9876543210", updated.Phone);
            Assert.Equal("456 New Ave", updated.Address);
        }
        finally
        {
            Cleanup(provider, dbName, webRoot);
        }
    }

    [Fact]
    public async Task ProfileImageUpload_ValidJpg_Succeeds()
    {
        var (provider, dbName, webRoot) = SetupServices();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();

            var user = new ApplicationUser { UserName = "jpguser@test.com", Email = "jpguser@test.com" };
            await userManager.CreateAsync(user);
            db.CustomerProfiles.Add(new CustomerProfile { UserId = user.Id, FullName = "JPG User" });
            await db.SaveChangesAsync();

            byte[] jpgBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];
            using var stream = new MemoryStream(jpgBytes);

            var result = await customerService.UploadProfileImageAsync(user.Id, stream, "avatar.jpg", "image/jpeg", jpgBytes.Length);

            Assert.NotNull(result.ProfileImageUrl);
            Assert.Contains("/uploads/profile-images/", result.ProfileImageUrl);
            Assert.EndsWith(".jpg", result.ProfileImageUrl);

            // Physical file check
            var savedPath = Path.Combine(webRoot, result.ProfileImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(savedPath));
        }
        finally
        {
            Cleanup(provider, dbName, webRoot);
        }
    }

    [Fact]
    public async Task ProfileImageUpload_ValidPngAndWebp_Succeeds()
    {
        var (provider, dbName, webRoot) = SetupServices();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();

            var user = new ApplicationUser { UserName = "pnguser@test.com", Email = "pnguser@test.com" };
            await userManager.CreateAsync(user);
            db.CustomerProfiles.Add(new CustomerProfile { UserId = user.Id, FullName = "PNG User" });
            await db.SaveChangesAsync();

            byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            using var pngStream = new MemoryStream(pngBytes);

            var result = await customerService.UploadProfileImageAsync(user.Id, pngStream, "avatar.png", "image/png", pngBytes.Length);

            Assert.NotNull(result.ProfileImageUrl);
            Assert.EndsWith(".png", result.ProfileImageUrl);

            // WEBP
            byte[] webpBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
            using var webpStream = new MemoryStream(webpBytes);

            var webpResult = await customerService.UploadProfileImageAsync(user.Id, webpStream, "avatar.webp", "image/webp", webpBytes.Length);

            Assert.NotNull(webpResult.ProfileImageUrl);
            Assert.EndsWith(".webp", webpResult.ProfileImageUrl);
        }
        finally
        {
            Cleanup(provider, dbName, webRoot);
        }
    }

    [Fact]
    public async Task ProfileImageUpload_InvalidFile_ThrowsException()
    {
        var (provider, dbName, webRoot) = SetupServices();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();

            var user = new ApplicationUser { UserName = "invaliduser@test.com", Email = "invaliduser@test.com" };
            await userManager.CreateAsync(user);
            db.CustomerProfiles.Add(new CustomerProfile { UserId = user.Id, FullName = "Invalid User" });
            await db.SaveChangesAsync();

            // Fake exe or pdf
            byte[] badBytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 header contents");
            using var stream = new MemoryStream(badBytes);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                customerService.UploadProfileImageAsync(user.Id, stream, "script.exe", "application/x-msdownload", badBytes.Length));

            Assert.Contains("Image must be JPG, PNG or WEBP.", ex.Message);
        }
        finally
        {
            Cleanup(provider, dbName, webRoot);
        }
    }

    [Fact]
    public async Task ProfileImageUpload_OversizedFile_ThrowsException()
    {
        var (provider, dbName, webRoot) = SetupServices();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();

            var user = new ApplicationUser { UserName = "biguser@test.com", Email = "biguser@test.com" };
            await userManager.CreateAsync(user);
            db.CustomerProfiles.Add(new CustomerProfile { UserId = user.Id, FullName = "Big User" });
            await db.SaveChangesAsync();

            byte[] dummyHeader = [0xFF, 0xD8, 0xFF, 0xE0];
            using var stream = new MemoryStream(dummyHeader);

            long oversizedLength = (5 * 1024 * 1024) + 1; // 5 MB + 1 byte

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                customerService.UploadProfileImageAsync(user.Id, stream, "large.jpg", "image/jpeg", oversizedLength));

            Assert.Contains("Image size must be less than 5 MB.", ex.Message);
        }
        finally
        {
            Cleanup(provider, dbName, webRoot);
        }
    }

    [Fact]
    public async Task ProfileImage_ReplacementAndDelete_RemovesOldFile()
    {
        var (provider, dbName, webRoot) = SetupServices();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();

            var user = new ApplicationUser { UserName = "replaceuser@test.com", Email = "replaceuser@test.com" };
            await userManager.CreateAsync(user);
            db.CustomerProfiles.Add(new CustomerProfile { UserId = user.Id, FullName = "Replace User" });
            await db.SaveChangesAsync();

            // First image upload
            byte[] jpgBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
            using var stream1 = new MemoryStream(jpgBytes);
            var res1 = await customerService.UploadProfileImageAsync(user.Id, stream1, "pic1.jpg", "image/jpeg", jpgBytes.Length);

            var path1 = Path.Combine(webRoot, res1.ProfileImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path1));

            // Second image upload (replacement)
            byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A];
            using var stream2 = new MemoryStream(pngBytes);
            var res2 = await customerService.UploadProfileImageAsync(user.Id, stream2, "pic2.png", "image/png", pngBytes.Length);

            var path2 = Path.Combine(webRoot, res2.ProfileImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(path2));
            Assert.False(File.Exists(path1)); // Old file should be deleted!

            // Remove image
            var removedRes = await customerService.RemoveProfileImageAsync(user.Id);
            Assert.Null(removedRes.ProfileImageUrl);
            Assert.False(File.Exists(path2)); // Current file should be deleted!
        }
        finally
        {
            Cleanup(provider, dbName, webRoot);
        }
    }
}
