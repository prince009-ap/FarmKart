using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class FarmerCropTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public FarmerCropTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_FarmerCropTest_{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:Secret", "ThisIsADevelopmentSecretKeyForTestingOnlyAndMustBeAtLeast32Bytes!" },
                    { "JwtSettings:Issuer", "FarmKart" },
                    { "JwtSettings:Audience", "FarmKartUsers" },
                    { "JwtSettings:ExpiryMinutes", "60" },
                    { "JwtSettings:CookieName", "FarmKartAuth" },
                    { "JwtSettings:CookieSecure", "false" },
                    { "JwtSettings:CookieSameSite", "Lax" }
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<FarmKartDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<FarmKartDbContext>(options =>
                    options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True"));

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                context.Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        db.Database.EnsureDeleted();
    }

    private async Task SetupTestUserAsync(string email, string password, string fullName, string role)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        if (role == Roles.Farmer)
        {
            await authService.RegisterFarmerAsync(new FarmerRegisterRequest(
                fullName, email, password, "1234567890", null, "123 Farm Road", "Happy Farm", 10.5m, FarmSizeUnit.Vigha, "Near Valley"));
        }
        else if (role == Roles.Worker)
        {
            await authService.RegisterWorkerAsync(new WorkerRegisterRequest(
                fullName, email, password, "1234567890", null, "123 Worker Road", 2, 100));
        }
        else if (role == Roles.Customer)
        {
            await authService.RegisterCustomerAsync(new CustomerRegisterRequest(
                fullName, email, password, "1234567890", null, "123 Customer Road"));
        }
    }

    private async Task<HttpClient> GetAuthenticatedClientAsync(string email, string password, string fullName, string role)
    {
        await SetupTestUserAsync(email, password, fullName, role);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var cookieHeader = loginResponse.Headers.GetValues("Set-Cookie").First();
        var nameValuePair = cookieHeader.Split(';').First(p => p.Trim().StartsWith("FarmKartAuth=")).Trim();

        var authenticatedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", nameValuePair);

        return authenticatedClient;
    }

    private static MultipartFormDataContent CreateImageContent(byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Test01_Farmer_Can_Create_Crop()
    {
        var client = await GetAuthenticatedClientAsync("farmer_create_crop@test.com", "Password123!", "Farmer Joe", Roles.Farmer);

        var request = new CreateCropRequest(
            CropName: "Wheat Special",
            CropType: "Cereal",
            Variety: "GW-322",
            Area: 5.5m,
            AreaUnit: "Bigha",
            SowingDate: new DateOnly(2026, 8, 1),
            ExpectedHarvestDate: new DateOnly(2026, 11, 15),
            ActualHarvestDate: null,
            Status: "Growing",
            Description: "Rabi season wheat"
        );

        var response = await client.PostAsJsonAsync("/api/farmer/crops", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var crop = await response.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);
        Assert.NotEqual(Guid.Empty, crop.Id);
        Assert.Equal("Wheat Special", crop.CropName);
        Assert.Equal("Cereal", crop.CropType);
        Assert.Equal("GW-322", crop.Variety);
        Assert.Equal(5.5m, crop.Area);
        Assert.Equal("Bigha", crop.AreaUnit);
        Assert.Equal("Growing", crop.Status);
    }

    [Fact]
    public async Task Test02_Crop_Is_Associated_With_Authenticated_Farmer()
    {
        var client = await GetAuthenticatedClientAsync("farmer_assoc_crop@test.com", "Password123!", "Farmer Assoc", Roles.Farmer);

        var request = new CreateCropRequest(
            CropName: "Cotton Supreme",
            CropType: "Commercial",
            Variety: "BT-2",
            Area: 10m,
            AreaUnit: "Acre",
            SowingDate: new DateOnly(2026, 6, 1),
            ExpectedHarvestDate: new DateOnly(2026, 12, 1),
            ActualHarvestDate: null,
            Status: "Planned",
            Description: null
        );

        var createResponse = await client.PostAsJsonAsync("/api/farmer/crops", request);
        var created = await createResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var dbCrop = await db.Crops.FindAsync(created.Id);
        Assert.NotNull(dbCrop);
        Assert.Equal(created.FarmerProfileId, dbCrop.FarmerProfileId);
    }

    [Fact]
    public async Task Test03_Farmer_Can_Retrieve_Own_Crops()
    {
        var client = await GetAuthenticatedClientAsync("farmer_list_crops@test.com", "Password123!", "Farmer List", Roles.Farmer);

        var req1 = new CreateCropRequest("Rice", "Cereal", "Basmati", 3m, "Acre", null, null, null, "Planned", null);
        var req2 = new CreateCropRequest("Sugarcane", "Cash Crop", "Co 0238", 8m, "Hectare", null, null, null, "Growing", null);

        await client.PostAsJsonAsync("/api/farmer/crops", req1);
        await client.PostAsJsonAsync("/api/farmer/crops", req2);

        var listResponse = await client.GetAsync("/api/farmer/crops");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var crops = await listResponse.Content.ReadFromJsonAsync<List<CropResponse>>();
        Assert.NotNull(crops);
        Assert.True(crops.Count >= 2);
        Assert.Contains(crops, c => c.CropName == "Rice");
        Assert.Contains(crops, c => c.CropName == "Sugarcane");
    }

    [Fact]
    public async Task Test04_Farmer_Can_Retrieve_Own_Crop_Details()
    {
        var client = await GetAuthenticatedClientAsync("farmer_details_crop@test.com", "Password123!", "Farmer Details", Roles.Farmer);

        var req = new CreateCropRequest("Maize Gold", "Cereal", "HQPM-1", 4m, "Bigha", new DateOnly(2026, 7, 10), new DateOnly(2026, 10, 20), null, "Growing", "Sweet corn");
        var createResponse = await client.PostAsJsonAsync("/api/farmer/crops", req);
        var created = await createResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(created);

        var detailsResponse = await client.GetAsync($"/api/farmer/crops/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var fetched = await detailsResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Maize Gold", fetched.CropName);
        Assert.Equal("Bigha", fetched.AreaUnit);
    }

    [Fact]
    public async Task Test05_Farmer_Can_Update_Own_Crop()
    {
        var client = await GetAuthenticatedClientAsync("farmer_update_crop@test.com", "Password123!", "Farmer Update", Roles.Farmer);

        var req = new CreateCropRequest("Mustard Yellow", "Oilseed", "Pusa Bold", 2m, "Bigha", new DateOnly(2026, 10, 1), new DateOnly(2027, 2, 15), null, "Planned", "Initial");
        var createResponse = await client.PostAsJsonAsync("/api/farmer/crops", req);
        var created = await createResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(created);

        var updateReq = new UpdateCropRequest("Mustard Yellow Modified", "Oilseed", "Pusa Bold", 3.5m, "Bigha", new DateOnly(2026, 10, 1), new DateOnly(2027, 2, 15), new DateOnly(2027, 2, 14), "Harvested", "Harvest completed early");
        var updateResponse = await client.PutAsJsonAsync($"/api/farmer/crops/{created.Id}", updateReq);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Mustard Yellow Modified", updated.CropName);
        Assert.Equal(3.5m, updated.Area);
        Assert.Equal("Harvested", updated.Status);
        Assert.Equal(new DateOnly(2027, 2, 14), updated.ActualHarvestDate);
    }

    [Fact]
    public async Task Test06_Farmer_Can_Delete_Own_Crop()
    {
        var client = await GetAuthenticatedClientAsync("farmer_delete_crop@test.com", "Password123!", "Farmer Delete", Roles.Farmer);

        var req = new CreateCropRequest("Potato Commercial", "Vegetable", "Kufri Jyoti", 1.5m, "Acre", null, null, null, "Planned", null);
        var createResponse = await client.PostAsJsonAsync("/api/farmer/crops", req);
        var created = await createResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(created);

        var deleteResponse = await client.DeleteAsync($"/api/farmer/crops/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/farmer/crops/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Test07_Farmer_Cannot_Retrieve_Another_Farmers_Crop()
    {
        var farmerA = await GetAuthenticatedClientAsync("farmerA_get_crop@test.com", "Password123!", "Farmer A", Roles.Farmer);
        var farmerB = await GetAuthenticatedClientAsync("farmerB_get_crop@test.com", "Password123!", "Farmer B", Roles.Farmer);

        var req = new CreateCropRequest("Farmer A Crop", "Pulses", "Chickpea", 5m, "Acre", null, null, null, "Planned", null);
        var createResponse = await farmerA.PostAsJsonAsync("/api/farmer/crops", req);
        var created = await createResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(created);

        var response = await farmerB.GetAsync($"/api/farmer/crops/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test08_Farmer_Cannot_Update_Another_Farmers_Crop()
    {
        var farmerA = await GetAuthenticatedClientAsync("farmerA_put_crop@test.com", "Password123!", "Farmer A", Roles.Farmer);
        var farmerB = await GetAuthenticatedClientAsync("farmerB_put_crop@test.com", "Password123!", "Farmer B", Roles.Farmer);

        var req = new CreateCropRequest("Farmer A Groundnut", "Oilseed", "TG-37A", 4m, "Acre", null, null, null, "Planned", null);
        var createResponse = await farmerA.PostAsJsonAsync("/api/farmer/crops", req);
        var created = await createResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(created);

        var updateReq = new UpdateCropRequest("Hacked Crop", "Oilseed", "Hacked", 99m, "Acre", null, null, null, "Planned", null);
        var response = await farmerB.PutAsJsonAsync($"/api/farmer/crops/{created.Id}", updateReq);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test09_Farmer_Cannot_Delete_Another_Farmers_Crop()
    {
        var farmerA = await GetAuthenticatedClientAsync("farmerA_del_crop@test.com", "Password123!", "Farmer A", Roles.Farmer);
        var farmerB = await GetAuthenticatedClientAsync("farmerB_del_crop@test.com", "Password123!", "Farmer B", Roles.Farmer);

        var req = new CreateCropRequest("Farmer A Tomato", "Vegetable", "Pusa Ruby", 2m, "Acre", null, null, null, "Planned", null);
        var createResponse = await farmerA.PostAsJsonAsync("/api/farmer/crops", req);
        var created = await createResponse.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(created);

        var response = await farmerB.DeleteAsync($"/api/farmer/crops/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test10_Worker_Cannot_Access_Farmer_Crop_APIs()
    {
        var workerClient = await GetAuthenticatedClientAsync("worker_crop_access@test.com", "Password123!", "Worker Sam", Roles.Worker);

        var response = await workerClient.GetAsync("/api/farmer/crops");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Test11_Customer_Cannot_Access_Farmer_Crop_APIs()
    {
        var customerClient = await GetAuthenticatedClientAsync("customer_crop_access@test.com", "Password123!", "Customer Dan", Roles.Customer);

        var response = await customerClient.GetAsync("/api/farmer/crops");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Test12_Unauthenticated_User_Receives_401()
    {
        var unauthClient = _factory.CreateClient();

        var response = await unauthClient.GetAsync("/api/farmer/crops");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // CROP IMAGE MANAGEMENT INTEGRATION TESTS (TDD)
    // =========================================================================

    [Fact]
    public async Task Test18_Farmer_Can_Upload_Crop_Image()
    {
        var client = await GetAuthenticatedClientAsync("farmer_img1@test.com", "Password123!", "Farmer Img1", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Wheat Img Test", "Cereal", null, 5m, "Bigha", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        var dummyImageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 }; // Fake JPG
        var content = CreateImageContent(dummyImageBytes, "test_crop.jpg", "image/jpeg");

        var uploadRes = await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", content);

        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);
        var imageRes = await uploadRes.Content.ReadFromJsonAsync<CropImageResponse>();
        Assert.NotNull(imageRes);
        Assert.Equal(crop.Id, imageRes.CropId);
        Assert.True(imageRes.IsPrimary); // First image should default to primary
        Assert.StartsWith("/uploads/crops/", imageRes.ImageUrl);
    }

    [Fact]
    public async Task Test19_Farmer_Can_Upload_Multiple_Images()
    {
        var client = await GetAuthenticatedClientAsync("farmer_img2@test.com", "Password123!", "Farmer Img2", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Rice Img Multi", "Cereal", null, 5m, "Acre", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        var img1 = CreateImageContent(new byte[] { 1, 2, 3 }, "img1.png", "image/png");
        var img2 = CreateImageContent(new byte[] { 4, 5, 6 }, "img2.webp", "image/webp");

        var upload1 = await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", img1);
        var upload2 = await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", img2);

        Assert.Equal(HttpStatusCode.Created, upload1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, upload2.StatusCode);

        var detailsRes = await client.GetAsync($"/api/farmer/crops/{crop.Id}");
        var updatedCrop = await detailsRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(updatedCrop);
        Assert.Equal(2, updatedCrop.Images.Count);
    }

    [Fact]
    public async Task Test20_Invalid_File_Type_Is_Rejected()
    {
        var client = await GetAuthenticatedClientAsync("farmer_img3@test.com", "Password123!", "Farmer Img3", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Cotton Invalid Ext", "Commercial", null, 5m, "Acre", null, null, null, "Planned", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        var exeContent = CreateImageContent(new byte[] { 0x4D, 0x5A }, "hacked.exe", "application/octet-stream");

        var uploadRes = await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", exeContent);

        Assert.Equal(HttpStatusCode.BadRequest, uploadRes.StatusCode);
    }

    [Fact]
    public async Task Test21_Oversized_File_Is_Rejected()
    {
        var client = await GetAuthenticatedClientAsync("farmer_img4@test.com", "Password123!", "Farmer Img4", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Sugarcane Huge File", "Cash Crop", null, 5m, "Hectare", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        var oversizedBytes = new byte[6 * 1024 * 1024]; // 6 MB (limit is 5 MB)
        var content = CreateImageContent(oversizedBytes, "huge.jpg", "image/jpeg");

        var uploadRes = await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", content);

        Assert.Equal(HttpStatusCode.BadRequest, uploadRes.StatusCode);
    }

    [Fact]
    public async Task Test22_Crop_Image_Is_Linked_To_Correct_Crop()
    {
        var client = await GetAuthenticatedClientAsync("farmer_img5@test.com", "Password123!", "Farmer Img5", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Potato Image Link", "Vegetable", null, 2m, "Bigha", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        var content = CreateImageContent(new byte[] { 10, 20 }, "potato.png", "image/png");
        var uploadRes = await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", content);

        var imageRes = await uploadRes.Content.ReadFromJsonAsync<CropImageResponse>();
        Assert.NotNull(imageRes);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var dbImage = await db.CropImages.FindAsync(imageRes.Id);

        Assert.NotNull(dbImage);
        Assert.Equal(crop.Id, dbImage.CropId);
    }

    [Fact]
    public async Task Test23_Farmer_Cannot_Modify_Another_Farmers_Crop_Image()
    {
        var farmerA = await GetAuthenticatedClientAsync("farmerA_img_sec@test.com", "Password123!", "Farmer A", Roles.Farmer);
        var farmerB = await GetAuthenticatedClientAsync("farmerB_img_sec@test.com", "Password123!", "Farmer B", Roles.Farmer);

        var cropRes = await farmerA.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Farmer A Maize", "Cereal", null, 4m, "Acre", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        var content = CreateImageContent(new byte[] { 1, 2, 3 }, "hacked.jpg", "image/jpeg");
        var response = await farmerB.PostAsync($"/api/farmer/crops/{crop.Id}/images", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test24_Farmer_Can_Remove_Own_Crop_Image()
    {
        var client = await GetAuthenticatedClientAsync("farmer_img_del@test.com", "Password123!", "Farmer Del Img", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Tomato Image Delete", "Vegetable", null, 1m, "Acre", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        var content = CreateImageContent(new byte[] { 1, 2, 3 }, "tomato.jpg", "image/jpeg");
        var uploadRes = await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", content);
        var imageRes = await uploadRes.Content.ReadFromJsonAsync<CropImageResponse>();
        Assert.NotNull(imageRes);

        var deleteRes = await client.DeleteAsync($"/api/farmer/crops/{crop.Id}/images/{imageRes.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var detailsRes = await client.GetAsync($"/api/farmer/crops/{crop.Id}");
        var updatedCrop = await detailsRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(updatedCrop);
        Assert.Empty(updatedCrop.Images);
    }

    [Fact]
    public async Task Test25_Primary_Image_Works_Correctly()
    {
        var client = await GetAuthenticatedClientAsync("farmer_primary_img@test.com", "Password123!", "Farmer Primary Img", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Basmati Primary Test", "Cereal", null, 3m, "Acre", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        var img1 = await (await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", CreateImageContent(new byte[] { 1 }, "img1.jpg", "image/jpeg"))).Content.ReadFromJsonAsync<CropImageResponse>();
        var img2 = await (await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", CreateImageContent(new byte[] { 2 }, "img2.jpg", "image/jpeg"))).Content.ReadFromJsonAsync<CropImageResponse>();

        Assert.NotNull(img1);
        Assert.NotNull(img2);
        Assert.True(img1.IsPrimary);
        Assert.False(img2.IsPrimary);

        // Set img2 as primary
        var setPrimaryRes = await client.PutAsync($"/api/farmer/crops/{crop.Id}/images/{img2.Id}/primary", null);
        Assert.Equal(HttpStatusCode.OK, setPrimaryRes.StatusCode);

        var updatedCrop = await setPrimaryRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(updatedCrop);
        Assert.Equal(img2.ImageUrl, updatedCrop.PrimaryImageUrl);
    }

    [Fact]
    public async Task Test26_Crop_Details_Return_Image_URLs()
    {
        var client = await GetAuthenticatedClientAsync("farmer_img_details@test.com", "Password123!", "Farmer Details Img", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Mustard Gallery Test", "Oilseed", null, 2m, "Bigha", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", CreateImageContent(new byte[] { 10 }, "m1.jpg", "image/jpeg"));

        var detailsRes = await client.GetAsync($"/api/farmer/crops/{crop.Id}");
        var details = await detailsRes.Content.ReadFromJsonAsync<CropResponse>();

        Assert.NotNull(details);
        Assert.NotEmpty(details.Images);
        Assert.NotNull(details.PrimaryImageUrl);
    }

    [Fact]
    public async Task Test27_Crop_List_Returns_Primary_Image()
    {
        var client = await GetAuthenticatedClientAsync("farmer_img_list@test.com", "Password123!", "Farmer List Img", Roles.Farmer);

        var cropRes = await client.PostAsJsonAsync("/api/farmer/crops", new CreateCropRequest("Groundnut List Test", "Oilseed", null, 4m, "Acre", null, null, null, "Growing", null));
        var crop = await cropRes.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(crop);

        await client.PostAsync($"/api/farmer/crops/{crop.Id}/images", CreateImageContent(new byte[] { 20 }, "gn.jpg", "image/jpeg"));

        var listRes = await client.GetAsync("/api/farmer/crops");
        var list = await listRes.Content.ReadFromJsonAsync<List<CropResponse>>();

        Assert.NotNull(list);
        var target = list.FirstOrDefault(c => c.Id == crop.Id);
        Assert.NotNull(target);
        Assert.NotNull(target.PrimaryImageUrl);
    }

    [Fact]
    public async Task Test28_Unauthenticated_Upload_Is_Rejected()
    {
        var unauthClient = _factory.CreateClient();
        var content = CreateImageContent(new byte[] { 1, 2, 3 }, "test.jpg", "image/jpeg");

        var response = await unauthClient.PostAsync($"/api/farmer/crops/{Guid.NewGuid()}/images", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Test29_Worker_And_Customer_Cannot_Upload_Crop_Images()
    {
        var workerClient = await GetAuthenticatedClientAsync("worker_crop_img@test.com", "Password123!", "Worker Img", Roles.Worker);
        var customerClient = await GetAuthenticatedClientAsync("customer_crop_img@test.com", "Password123!", "Customer Img", Roles.Customer);

        var content1 = CreateImageContent(new byte[] { 1, 2 }, "w.jpg", "image/jpeg");
        var content2 = CreateImageContent(new byte[] { 3, 4 }, "c.jpg", "image/jpeg");

        var res1 = await workerClient.PostAsync($"/api/farmer/crops/{Guid.NewGuid()}/images", content1);
        var res2 = await customerClient.PostAsync($"/api/farmer/crops/{Guid.NewGuid()}/images", content2);

        Assert.Equal(HttpStatusCode.Forbidden, res1.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, res2.StatusCode);
    }
}
