using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class FarmerCropStockTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public FarmerCropStockTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_CropStockTest_{Guid.NewGuid()}";
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
                var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        db.Database.EnsureDeleted();
    }

    private async Task<(HttpClient Client, Crop HarvestedCrop, Crop GrowingCrop)> CreateFarmerAndCropsAsync(string emailPrefix = "farmer")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var email = $"{emailPrefix}_{Guid.NewGuid()}@farmkart.test";
        var password = "Password123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await authService.RegisterFarmerAsync(new FarmerRegisterRequest(
                "Test Farmer", email, password, "9876543210", null, "123 Farm Rd", "Green Farm", 10, FarmSizeUnit.Vigha, "Valley"));
        }

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginRes.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmer = await db.FarmerProfiles.FirstAsync(f => f.FullName == "Test Farmer" || f.Phone == "9876543210");

            var harvestedCrop = new Crop
            {
                FarmerProfileId = farmer.Id,
                CropName = "Harvested Wheat",
                CropType = "Cereal",
                Area = 5,
                AreaUnit = FarmSizeUnit.Vigha,
                Status = CropStatus.Harvested,
                Quantity = 0,
                Unit = MeasurementUnit.Kilogram
            };

            var growingCrop = new Crop
            {
                FarmerProfileId = farmer.Id,
                CropName = "Growing Rice",
                CropType = "Cereal",
                Area = 10,
                AreaUnit = FarmSizeUnit.Acre,
                Status = CropStatus.Growing,
                Quantity = 0,
                Unit = MeasurementUnit.Kilogram
            };

            db.Crops.AddRange(harvestedCrop, growingCrop);
            await db.SaveChangesAsync();

            return (client, harvestedCrop, growingCrop);
        }
    }

    private async Task<HttpClient> CreateUserInRoleAsync(string roleName)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var email = $"{roleName.ToLower()}_{Guid.NewGuid()}@farmkart.test";
        var password = "Password123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            if (roleName == Roles.Worker)
            {
                await authService.RegisterWorkerAsync(new WorkerRegisterRequest(
                    "Test Worker", email, password, "1112223334", null, "123 Worker Rd", 2, 100));
            }
            else if (roleName == Roles.Customer)
            {
                await authService.RegisterCustomerAsync(new CustomerRegisterRequest(
                    "Test Customer", email, password, "5556667778", null, "123 Customer Rd"));
            }
        }

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginRes.EnsureSuccessStatusCode();

        return client;
    }

    // ================================================================
    // ORIGINAL TESTS (Test01–Test15) — unchanged
    // ================================================================

    [Fact]
    public async Task Test01_Farmer_Can_Add_Stock_To_Harvested_Crop()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        var request = new AddCropStockRequest(500, "Kilogram", "Harvest", "Harvested from field A");
        var response = await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(500m, summary.AvailableQuantityKg);
        Assert.Equal("5 Quintals", summary.AvailableQuantityFormatted);
        Assert.Equal(1, summary.TotalTransactionsCount);
    }

    [Fact]
    public async Task Test02_Stock_Is_Associated_With_Correct_Crop()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        var request = new AddCropStockRequest(300, "Kg", "Harvest", "Batch 1");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", request);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var stockInDb = await db.CropStockTransactions.FirstOrDefaultAsync(t => t.CropId == crop.Id);
        Assert.NotNull(stockInDb);
        Assert.Equal(300m, stockInDb.Quantity);
        Assert.Equal("Batch 1", stockInDb.Notes);
    }

    [Fact]
    public async Task Test03_Farmer_Can_Retrieve_Own_Crop_Stock_Summary()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(250, "Kg"));

        var response = await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(crop.Id, summary.CropId);
        Assert.Equal(250m, summary.AvailableQuantityKg);
    }

    [Fact]
    public async Task Test04_Farmer_Can_Add_Additional_Stock_And_Calculate_Total()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(500, "Kg"));
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(250, "Kg"));

        var response = await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock");
        var summary = await response.Content.ReadFromJsonAsync<CropStockSummaryResponse>();

        Assert.NotNull(summary);
        Assert.Equal(750m, summary.AvailableQuantityKg);
        Assert.Equal("750 Kg", summary.AvailableQuantityFormatted);
        Assert.Equal(2, summary.TotalTransactionsCount);
    }

    [Fact]
    public async Task Test05_Unit_Conversions_Calculate_Correctly_Kg_Quintal_Ton()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        // 500 Kg + 2 Tons (2000 Kg) = 2500 Kg
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(500, "Kilogram"));
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(2, "Ton"));

        var response = await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock");
        var summary = await response.Content.ReadFromJsonAsync<CropStockSummaryResponse>();

        Assert.NotNull(summary);
        Assert.Equal(2500m, summary.AvailableQuantityKg);
        Assert.Equal("2.5 Tons", summary.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test06_Farmer_Can_View_Stock_History()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(500, "Kg", "Harvest", "First cut"));
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(1, "Quintal", "Harvest", "Second cut"));

        var response = await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await response.Content.ReadFromJsonAsync<List<CropStockTransactionResponse>>();
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Notes == "First cut");
        Assert.Contains(history, h => h.Notes == "Second cut");
    }

    [Fact]
    public async Task Test07_Quantity_Must_Be_Greater_Than_Zero()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        var request = new AddCropStockRequest(0, "Kg");
        var response = await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test08_Invalid_Unit_Rejected()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        var request = new AddCropStockRequest(100, "InvalidUnitName");
        var response = await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test09_Negative_Resulting_Stock_Rejected()
    {
        var (client, crop, _) = await CreateFarmerAndCropsAsync();

        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(100, "Kg"));

        // Attempting adjustment of -200 Kg when total is 100 Kg
        var adjustReq = new AdjustCropStockRequest(-200, "Kg", "Correction overflow");
        var response = await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock/adjust", adjustReq);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test10_Nonexistent_Crop_Returns_NotFound()
    {
        var (client, _, _) = await CreateFarmerAndCropsAsync();

        var fakeCropId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/farmer/crops/{fakeCropId}/stock");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test11_Farmer_Cannot_Manage_Another_Farmers_Crop_Stock()
    {
        var (clientFarmer1, farmer1Crop, _) = await CreateFarmerAndCropsAsync("farmer1");
        var (clientFarmer2, _, _) = await CreateFarmerAndCropsAsync("farmer2");

        // Farmer 2 attempts to add stock to Farmer 1's crop
        var response = await clientFarmer2.PostAsJsonAsync($"/api/farmer/crops/{farmer1Crop.Id}/stock", new AddCropStockRequest(500, "Kg"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test12_Worker_Cannot_Manage_Farmer_Stock()
    {
        var (_, farmerCrop, _) = await CreateFarmerAndCropsAsync();
        var workerClient = await CreateUserInRoleAsync(Roles.Worker);

        var response = await workerClient.GetAsync($"/api/farmer/crops/{farmerCrop.Id}/stock");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Test13_Customer_Cannot_Manage_Farmer_Stock()
    {
        var (_, farmerCrop, _) = await CreateFarmerAndCropsAsync();
        var customerClient = await CreateUserInRoleAsync(Roles.Customer);

        var response = await customerClient.GetAsync($"/api/farmer/crops/{farmerCrop.Id}/stock");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Test14_Unauthenticated_Request_Rejected()
    {
        var (_, farmerCrop, _) = await CreateFarmerAndCropsAsync();
        var unauthClient = _factory.CreateClient();

        var response = await unauthClient.GetAsync($"/api/farmer/crops/{farmerCrop.Id}/stock");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Test15_Growing_Or_Planned_Crop_Cannot_Receive_Harvested_Stock()
    {
        var (client, _, growingCrop) = await CreateFarmerAndCropsAsync();

        var request = new AddCropStockRequest(500, "Kg", "Harvest");
        var response = await client.PostAsJsonAsync($"/api/farmer/crops/{growingCrop.Id}/stock", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ================================================================
    // TDD TESTS: Stock Sync — Crop List DTO Must Include Available Stock
    // ================================================================

    [Fact]
    public async Task Test16_CropList_Returns_AvailableStock_For_Harvested_Crop()
    {
        // After adding 500 Kg, GET /api/farmer/crops must return availableQuantityKg = 500
        var (client, crop, _) = await CreateFarmerAndCropsAsync("list_stock_test");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(500, "Kilogram"));

        var listResponse = await client.GetAsync("/api/farmer/crops");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var crops = await listResponse.Content.ReadFromJsonAsync<List<CropResponse>>();
        Assert.NotNull(crops);

        var harvestedCrop = crops.FirstOrDefault(c => c.Id == crop.Id);
        Assert.NotNull(harvestedCrop);
        Assert.Equal(500m, harvestedCrop.AvailableQuantityKg);
        Assert.Equal("5 Quintals", harvestedCrop.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test17_CropList_Shows_Zero_Stock_For_Crop_With_No_Transactions()
    {
        // A crop with no stock transactions should return availableQuantityKg = 0
        var (client, crop, _) = await CreateFarmerAndCropsAsync("zero_stock_test");
        // Do NOT add any stock

        var listResponse = await client.GetAsync("/api/farmer/crops");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var crops = await listResponse.Content.ReadFromJsonAsync<List<CropResponse>>();
        Assert.NotNull(crops);

        var targetCrop = crops.FirstOrDefault(c => c.Id == crop.Id);
        Assert.NotNull(targetCrop);
        Assert.Equal(0m, targetCrop.AvailableQuantityKg);
        Assert.Equal("0 Kg", targetCrop.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test18_CropList_Stock_Updates_After_Adding_More_Stock()
    {
        // Add 500 Kg → verify list → add 250 Kg → verify list again (750 Kg)
        var (client, crop, _) = await CreateFarmerAndCropsAsync("stock_update_test");

        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(500, "Kilogram"));

        var list1 = await (await client.GetAsync("/api/farmer/crops")).Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry1 = list1?.FirstOrDefault(c => c.Id == crop.Id);
        Assert.NotNull(entry1);
        Assert.Equal(500m, entry1.AvailableQuantityKg);

        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(250, "Kilogram"));

        var list2 = await (await client.GetAsync("/api/farmer/crops")).Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry2 = list2?.FirstOrDefault(c => c.Id == crop.Id);
        Assert.NotNull(entry2);
        Assert.Equal(750m, entry2.AvailableQuantityKg);
        Assert.Equal("750 Kg", entry2.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test19_StockDetail_And_CropList_Show_Same_AvailableQuantity()
    {
        // Stock Detail endpoint and Crop List endpoint must agree on available quantity
        var (client, crop, _) = await CreateFarmerAndCropsAsync("sync_test");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(5, "Quintal"));

        var stockDetail = await (await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();

        var cropList = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var cropEntry = cropList?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(stockDetail);
        Assert.NotNull(cropEntry);
        // Both endpoints must agree
        Assert.Equal(stockDetail.AvailableQuantityKg, cropEntry.AvailableQuantityKg);
        Assert.Equal(stockDetail.AvailableQuantityFormatted, cropEntry.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test20_Multiple_Crops_Show_Independent_Stock()
    {
        // Two crops of the same farmer must carry independent stock values
        var (client, harvestedCrop, _) = await CreateFarmerAndCropsAsync("multi_crop_test");
        Guid secondCropId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmer = await db.FarmerProfiles.FirstAsync(f => f.Crops.Any(c => c.Id == harvestedCrop.Id));
            var secondCrop = new Crop
            {
                FarmerProfileId = farmer.Id,
                CropName = "Rice Harvested",
                CropType = "Grain",
                Area = 3,
                AreaUnit = FarmSizeUnit.Acre,
                Status = CropStatus.Harvested,
                Quantity = 0,
                Unit = MeasurementUnit.Kilogram
            };
            db.Crops.Add(secondCrop);
            await db.SaveChangesAsync();
            secondCropId = secondCrop.Id;
        }

        await client.PostAsJsonAsync($"/api/farmer/crops/{harvestedCrop.Id}/stock", new AddCropStockRequest(200, "Kilogram"));
        await client.PostAsJsonAsync($"/api/farmer/crops/{secondCropId}/stock", new AddCropStockRequest(1, "Ton"));

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        Assert.NotNull(list);

        var first = list.FirstOrDefault(c => c.Id == harvestedCrop.Id);
        var second = list.FirstOrDefault(c => c.Id == secondCropId);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(200m, first.AvailableQuantityKg);
        Assert.Equal(1000m, second.AvailableQuantityKg);
    }

    [Fact]
    public async Task Test21_Farmer_Cannot_See_Another_Farmers_Stock_Via_CropList()
    {
        // Farmer2's crop list must not include Farmer1's crops
        var (clientFarmer1, farmer1Crop, _) = await CreateFarmerAndCropsAsync("isolation_f1");
        var (clientFarmer2, _, _) = await CreateFarmerAndCropsAsync("isolation_f2");

        await clientFarmer1.PostAsJsonAsync($"/api/farmer/crops/{farmer1Crop.Id}/stock", new AddCropStockRequest(300, "Kg"));

        var farmer2List = await (await clientFarmer2.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        Assert.NotNull(farmer2List);

        // Farmer2 should not see Farmer1's crop in their list
        Assert.DoesNotContain(farmer2List, c => c.Id == farmer1Crop.Id);
    }

    [Fact]
    public async Task Test22_CropDetail_Returns_AvailableStock()
    {
        // GET /api/farmer/crops/{id} must also return correct availableQuantityKg
        var (client, crop, _) = await CreateFarmerAndCropsAsync("detail_stock_test");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(2, "Ton"));

        var response = await client.GetAsync($"/api/farmer/crops/{crop.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<CropResponse>();
        Assert.NotNull(detail);
        Assert.Equal(2000m, detail.AvailableQuantityKg);
        Assert.Equal("2 Tons", detail.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test23_QuintalUnit_Formatted_Correctly_In_CropList()
    {
        // 5 Quintals = 500 Kg → should format as "5 Quintals"
        var (client, crop, _) = await CreateFarmerAndCropsAsync("quintal_fmt_test");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock", new AddCropStockRequest(5, "Quintal"));

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry = list?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(entry);
        Assert.Equal(500m, entry.AvailableQuantityKg);
        Assert.Equal("5 Quintals", entry.AvailableQuantityFormatted);
    }

    // ================================================================
    // TDD: Unit Conversion Regression Tests (Test24–Test32)
    // Ensure crop card always reads from transaction sum, never from
    // a potentially stale crop.Quantity cached field.
    //
    // Mathematical reference:
    //   1 Quintal = 100 Kg
    //   1 Ton     = 1000 Kg
    // ================================================================

    [Fact]
    public async Task Test24_CropCard_Shows_5_Quintals_For_500_Kg_Transaction()
    {
        // Regression: 500 Kg must NEVER display as "1 Ton" on the crop card.
        // The card must read from transaction sum (500 Kg), not from any
        // cached/stale crop.Quantity field that could hold a wrong value.
        var (client, crop, _) = await CreateFarmerAndCropsAsync("unit_500kg");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(500, "Kilogram"));

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry = list?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(entry);
        Assert.Equal(500m, entry.AvailableQuantityKg);
        // 500 Kg = 5 Quintals — must NOT be "1 Ton" or any other value
        Assert.Equal("5 Quintals", entry.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test25_CropCard_And_StockDetails_Agree_For_500_Kg()
    {
        // The crop card and stock details must represent the exact same quantity.
        var (client, crop, _) = await CreateFarmerAndCropsAsync("sync_500kg");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(500, "Kilogram"));

        var stockDetail = await (await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var cardEntry = list?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(stockDetail);
        Assert.NotNull(cardEntry);
        Assert.Equal(500m, stockDetail.AvailableQuantityKg);
        Assert.Equal(500m, cardEntry.AvailableQuantityKg);
        Assert.Equal(stockDetail.AvailableQuantityKg, cardEntry.AvailableQuantityKg);
        Assert.Equal(stockDetail.AvailableQuantityFormatted, cardEntry.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test26_1000_Kg_Formats_As_1_Ton()
    {
        // 1000 Kg = 1 Ton
        var (client, crop, _) = await CreateFarmerAndCropsAsync("unit_1000kg");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(1000, "Kilogram"));

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry = list?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(entry);
        Assert.Equal(1000m, entry.AvailableQuantityKg);
        Assert.Equal("1 Ton", entry.AvailableQuantityFormatted);

        // Verify stock detail agrees
        var stockDetail = await (await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(stockDetail);
        Assert.Equal(1000m, stockDetail.AvailableQuantityKg);
        Assert.Equal("1 Ton", stockDetail.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test27_1500_Kg_Formats_As_1_Point_5_Tons()
    {
        // 1500 Kg = 1.5 Tons
        var (client, crop, _) = await CreateFarmerAndCropsAsync("unit_1500kg");
        // 500 Kg + 1000 Kg = 1500 Kg
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(500, "Kilogram"));
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(1000, "Kilogram"));

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry = list?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(entry);
        Assert.Equal(1500m, entry.AvailableQuantityKg);
        Assert.Equal("1.5 Tons", entry.AvailableQuantityFormatted);

        var stockDetail = await (await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(stockDetail);
        Assert.Equal(1500m, stockDetail.AvailableQuantityKg);
        Assert.Equal("1.5 Tons", stockDetail.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test28_10_Quintals_Equals_1000_Kg_Formats_As_1_Ton()
    {
        // 10 Quintals = 1000 Kg = 1 Ton
        var (client, crop, _) = await CreateFarmerAndCropsAsync("unit_10q");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(10, "Quintal"));

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry = list?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(entry);
        Assert.Equal(1000m, entry.AvailableQuantityKg);
        Assert.Equal("1 Ton", entry.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test29_CropCard_Shows_Correct_Value_After_Second_Stock_Entry()
    {
        // Adding another 500 Kg to an existing 500 Kg crop must show 1000 Kg = 1 Ton,
        // not 2 Tons (which would indicate double-counting).
        var (client, crop, _) = await CreateFarmerAndCropsAsync("cumulative_test");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(500, "Kilogram"));
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(500, "Kilogram"));

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry = list?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(entry);
        // 500 + 500 = 1000 Kg, NOT 2000 (which would be double-counted twice)
        Assert.Equal(1000m, entry.AvailableQuantityKg);
        Assert.Equal("1 Ton", entry.AvailableQuantityFormatted);

        var stockDetail = await (await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(stockDetail);
        Assert.Equal(1000m, stockDetail.AvailableQuantityKg);
        Assert.Equal(entry.AvailableQuantityKg, stockDetail.AvailableQuantityKg);
    }

    [Fact]
    public async Task Test30_250_Kg_Formats_As_250_Kg()
    {
        // 250 Kg is not divisible into exact Quintals (2.5 Q) or Tons,
        // so it displays as "250 Kg" in both card and stock detail.
        var (client, crop, _) = await CreateFarmerAndCropsAsync("unit_250kg");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(250, "Kilogram"));

        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry = list?.FirstOrDefault(c => c.Id == crop.Id);

        Assert.NotNull(entry);
        Assert.Equal(250m, entry.AvailableQuantityKg);
        Assert.Equal("250 Kg", entry.AvailableQuantityFormatted);

        var stockDetail = await (await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(stockDetail);
        Assert.Equal(250m, stockDetail.AvailableQuantityKg);
        // Card and stock details agree on both value and format
        Assert.Equal(entry.AvailableQuantityKg, stockDetail.AvailableQuantityKg);
        Assert.Equal(entry.AvailableQuantityFormatted, stockDetail.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test31_Unit_Conversion_Math_Kg_To_Quintal_Is_Divide_By_100()
    {
        // Mathematical correctness: 1 Quintal = 100 Kg (not 50, not 200)
        // 300 Kg = 3 Quintals
        var (client, crop, _) = await CreateFarmerAndCropsAsync("math_quintal");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(300, "Kilogram"));

        var stockDetail = await (await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(stockDetail);
        Assert.Equal(300m, stockDetail.AvailableQuantityKg);
        Assert.Equal("3 Quintals", stockDetail.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test32_Unit_Conversion_Math_Kg_To_Ton_Is_Divide_By_1000()
    {
        // Mathematical correctness: 1 Ton = 1000 Kg (not 500)
        // 2000 Kg = 2 Tons
        var (client, crop, _) = await CreateFarmerAndCropsAsync("math_ton");
        await client.PostAsJsonAsync($"/api/farmer/crops/{crop.Id}/stock",
            new AddCropStockRequest(2, "Ton"));

        var stockDetail = await (await client.GetAsync($"/api/farmer/crops/{crop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(stockDetail);
        Assert.Equal(2000m, stockDetail.AvailableQuantityKg);
        Assert.Equal("2 Tons", stockDetail.AvailableQuantityFormatted);

        // Verify crop card also shows 2 Tons
        var list = await (await client.GetAsync("/api/farmer/crops"))
            .Content.ReadFromJsonAsync<List<CropResponse>>();
        var entry = list?.FirstOrDefault(c => c.Id == crop.Id);
        Assert.NotNull(entry);
        Assert.Equal(2000m, entry.AvailableQuantityKg);
        Assert.Equal("2 Tons", entry.AvailableQuantityFormatted);
    }

    [Fact]
    public async Task Test33_Fractional_Quintal_And_Ton_Inputs_Use_Correct_Kilogram_Factors()
    {
        // 2.5 Quintals and 0.25 Tons each represent exactly 250 Kg.
        var (client, firstCrop, _) = await CreateFarmerAndCropsAsync("fractional_quintal");
        await client.PostAsJsonAsync($"/api/farmer/crops/{firstCrop.Id}/stock",
            new AddCropStockRequest(2.5m, "Quintal"));

        var firstSummary = await (await client.GetAsync($"/api/farmer/crops/{firstCrop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(firstSummary);
        Assert.Equal(250m, firstSummary.AvailableQuantityKg);
        Assert.Equal("250 Kg", firstSummary.AvailableQuantityFormatted);

        var (secondClient, secondCrop, _) = await CreateFarmerAndCropsAsync("fractional_ton");
        await secondClient.PostAsJsonAsync($"/api/farmer/crops/{secondCrop.Id}/stock",
            new AddCropStockRequest(0.25m, "Ton"));

        var secondSummary = await (await secondClient.GetAsync($"/api/farmer/crops/{secondCrop.Id}/stock"))
            .Content.ReadFromJsonAsync<CropStockSummaryResponse>();
        Assert.NotNull(secondSummary);
        Assert.Equal(250m, secondSummary.AvailableQuantityKg);
        Assert.Equal("250 Kg", secondSummary.AvailableQuantityFormatted);
    }
}
