using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class MachineryRentalMarketplaceTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MachineryRentalMarketplaceTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_MachineryTest_{Guid.NewGuid()}";
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
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<FarmKartDbContext>(options =>
                    options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True"));

                using var sp = services.BuildServiceProvider();
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

    // ─── Test Helpers ─────────────────────────────────────────────────────────

    private async Task<(HttpClient client, string userId)> GetAuthenticatedClientAsync(string email, string role)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));

            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                await userManager.CreateAsync(user, "Password123!");
                await userManager.AddToRoleAsync(user, role);

                var userGuid = user.Id;
                if (role == Roles.Farmer)
                {
                    db.FarmerProfiles.Add(new FarmerProfile { UserId = userGuid, FullName = $"Farmer {email}", Phone = "9876543210", FarmName = "Test Farm", FarmLocation = "Rajkot" });
                }
                else if (role == Roles.Customer)
                {
                    db.CustomerProfiles.Add(new CustomerProfile { UserId = userGuid, FullName = $"Customer {email}", Phone = "9876543211" });
                }
                await db.SaveChangesAsync();
            }
        }

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            return (client, user!.Id.ToString());
        }
    }

    // ─── 30 Test Scenarios ───────────────────────────────────────────────────

    [Fact]
    public async Task Test01_Farmer_Can_Create_Machinery()
    {
        var (client, _) = await GetAuthenticatedClientAsync($"farmer01_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var req = new CreateMachineryRequest("Mahindra Tractor 1", "Tractor", "Mahindra", "575", 2022, "Tractor for rent", 1500, 500, false, false, false, 0, null, null, null, "Kalawad Road", "Rajkot", "Gujarat", "360005");

        var res = await client.PostAsJsonAsync("/api/my-machinery", req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var mach = await res.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);
        Assert.NotNull(mach);
        Assert.Equal("Mahindra Tractor 1", mach.Name);
    }

    [Fact]
    public async Task Test02_Customer_Can_Create_Machinery()
    {
        var (client, _) = await GetAuthenticatedClientAsync($"cust02_{Guid.NewGuid()}@test.com", Roles.Customer);
        var req = new CreateMachineryRequest("JCB Digger 2", "JCB", "JCB", "3DX", 2023, "JCB for heavy work", 3000, 1000, false, false, true, 800, "Ramesh Driver", "9998887776", "Experienced JCB driver", "Ring Road", "Surat", "Gujarat", "395001");

        var res = await client.PostAsJsonAsync("/api/my-machinery", req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var mach = await res.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);
        Assert.NotNull(mach);
        Assert.True(mach.DriverAvailable);
        Assert.Equal(800, mach.DriverChargePerDay);
    }

    [Fact]
    public async Task Test03_Farmer_Can_Rent_Customer_Machinery()
    {
        var (custClient, _) = await GetAuthenticatedClientAsync($"cust03_{Guid.NewGuid()}@test.com", Roles.Customer);
        var createRes = await custClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Customer JCB", "JCB", "JCB", "3DX", 2023, "JCB for rent", 2500, 500, false, false, true, 500, null, null, null, "Surat", "Surat", "Gujarat", "395001"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (farmerClient, _) = await GetAuthenticatedClientAsync($"farmer03_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var bookReq = new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), false, "UPI");

        var bookRes = await farmerClient.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", bookReq);
        Assert.Equal(HttpStatusCode.Created, bookRes.StatusCode);

        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);
        Assert.NotNull(rental);
        Assert.Equal(3, rental.RentalDays);
        Assert.Equal(0, rental.DriverAmount);
        Assert.Equal(7500, rental.MachineryAmount);
    }

    [Fact]
    public async Task Test04_Customer_Can_Rent_Farmer_Machinery()
    {
        var (farmerClient, _) = await GetAuthenticatedClientAsync($"farmer04_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await farmerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Farmer Tractor", "Tractor", "Sonalika", "DI 750", 2021, "Heavy tractor", 2000, 500, false, false, true, 600, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (custClient, _) = await GetAuthenticatedClientAsync($"cust04_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookReq = new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)), true, "Card");

        var bookRes = await custClient.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", bookReq);
        Assert.Equal(HttpStatusCode.Created, bookRes.StatusCode);

        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);
        Assert.NotNull(rental);
        Assert.True(rental.DriverRequired);
        Assert.Equal(1800, rental.DriverAmount); // 3 days * 600
        Assert.Equal(6000, rental.MachineryAmount); // 3 days * 2000
        Assert.Equal(7800, rental.TotalAmount);
    }

    [Fact]
    public async Task Test05_Farmer_Can_Rent_Farmer_Machinery()
    {
        var (farmerA, _) = await GetAuthenticatedClientAsync($"farmerA_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await farmerA.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Farmer A Rotavator", "Rotavator", "Shaktiman", "SR-7", 2022, "Rotavator", 1200, 300, false, false, false, 0, null, null, null, "Anand", "Anand", "Gujarat", "388001"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (farmerB, _) = await GetAuthenticatedClientAsync($"farmerB_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var bookRes = await farmerB.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), false, "UPI"));
        Assert.Equal(HttpStatusCode.Created, bookRes.StatusCode);
    }

    [Fact]
    public async Task Test06_Customer_Can_Rent_Customer_Machinery()
    {
        var (custA, _) = await GetAuthenticatedClientAsync($"custA_{Guid.NewGuid()}@test.com", Roles.Customer);
        var createRes = await custA.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Cust A Sprayer", "Sprayer", "Aspee", "HTP", 2023, "Power Sprayer", 800, 200, false, false, false, 0, null, null, null, "Vadodara", "Vadodara", "Gujarat", "390001"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (custB, _) = await GetAuthenticatedClientAsync($"custB_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await custB.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), false, "UPI"));
        Assert.Equal(HttpStatusCode.Created, bookRes.StatusCode);
    }

    [Fact]
    public async Task Test07_Owner_Cannot_Rent_Own_Machinery()
    {
        var (client, _) = await GetAuthenticatedClientAsync($"owner07_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await client.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Own Harvester", "Harvester", "Preet", "987", 2022, "Harvester", 4000, 1000, false, false, false, 0, null, null, null, "Surat", "Surat", "Gujarat", "395001"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var bookRes = await client.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), false, "UPI"));
        Assert.Equal(HttpStatusCode.Conflict, bookRes.StatusCode);
    }

    [Fact]
    public async Task Test08_DriverAvailable_False_Prevents_Driver_Selection()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner08_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("No Driver Tractor", "Tractor", "Eicher", "380", 2021, "Self drive only", 1500, 400, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter08_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), true, "UPI"));
        Assert.Equal(HttpStatusCode.BadRequest, bookRes.StatusCode);
    }

    [Fact]
    public async Task Test09_DriverAvailable_True_Allows_Driver_Selection()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner09_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Driver Available Tractor", "Tractor", "Sonalika", "DI 60", 2022, "With driver option", 1800, 500, false, false, true, 500, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter09_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), true, "UPI"));
        Assert.Equal(HttpStatusCode.Created, bookRes.StatusCode);
    }

    [Fact]
    public async Task Test10_DriverRequired_False_Produces_Zero_Driver_Charge()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner10_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Opt Driver Tractor", "Tractor", "Kubota", "MU5501", 2023, "Opt driver", 2200, 600, false, false, true, 700, null, null, null, "Anand", "Anand", "Gujarat", "388001"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter10_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), false, "UPI"));
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        Assert.Equal(0, rental!.DriverAmount);
        Assert.Equal(6600, rental.MachineryAmount); // 3 days * 2200
        Assert.Equal(6600, rental.TotalAmount);
    }

    [Fact]
    public async Task Test11_DriverRequired_True_Calculates_Driver_Charge()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner11_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Driver Calc Tractor", "Tractor", "New Holland", "3630", 2022, "With driver", 2000, 500, false, false, true, 500, null, null, null, "Surat", "Surat", "Gujarat", "395001"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter11_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), true, "UPI"));
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        Assert.Equal(1000, rental!.DriverAmount); // 2 days * 500
        Assert.Equal(4000, rental.MachineryAmount); // 2 days * 2000
        Assert.Equal(5000, rental.TotalAmount);
    }

    [Fact]
    public async Task Test12_Machinery_Rent_Calculated_Server_Side()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner12_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Server Calc 1", "Tractor", "John Deere", "5050D", 2021, "Desc", 1800, 500, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter12_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)), false, "UPI"));
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        Assert.Equal(4, rental!.RentalDays);
        Assert.Equal(7200, rental.MachineryAmount); // 4 * 1800
    }

    [Fact]
    public async Task Test13_Driver_Rent_Calculated_Server_Side()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner13_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Server Calc 2", "JCB", "JCB", "3DX", 2023, "JCB", 3000, 1000, false, false, true, 750, null, null, null, "Surat", "Surat", "Gujarat", "395001"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter13_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), true, "UPI"));
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        Assert.Equal(3, rental!.RentalDays);
        Assert.Equal(2250, rental.DriverAmount); // 3 * 750
    }

    [Fact]
    public async Task Test14_Total_Calculated_Server_Side()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner14_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Server Calc Total", "Tractor", "Farmtrac", "60", 2022, "Desc", 2000, 500, false, false, true, 500, null, null, null, "Anand", "Anand", "Gujarat", "388001"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter14_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), true, "UPI"));
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        Assert.Equal(6000, rental!.MachineryAmount);
        Assert.Equal(1500, rental.DriverAmount);
        Assert.Equal(7500, rental.TotalAmount);
        Assert.Equal(8000, rental.TotalPayableAmount); // 7500 + 500 deposit
    }

    [Fact]
    public async Task Test15_Price_Snapshots_Stored()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner15_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Snapshot Tractor", "Tractor", "Mahindra", "575", 2022, "Desc", 1500, 500, false, false, true, 400, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter15_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), true, "UPI"));
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        // Later owner updates prices
        await owner.PutAsJsonAsync($"/api/my-machinery/{mach.Id}", new UpdateMachineryRequest(DailyRent: 2500, DriverChargePerDay: 800, Name: "Snapshot Tractor"));

        // Fetch existing rental
        var getRentalRes = await renter.GetAsync($"/api/rentals/{rental!.Id}");
        var existingRental = await getRentalRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        Assert.Equal(1500, existingRental!.RentPerDaySnapshot);
        Assert.Equal(400, existingRental.DriverChargePerDaySnapshot);
        Assert.Equal(3800, existingRental.TotalAmount); // (1500+400)*2
    }

    [Fact]
    public async Task Test16_Search_By_Name_Works()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"search16_owner_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Super Cultivator 3000", "Cultivator", "Fieldking", "C1", 2022, "Desc", 1000, 200, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));

        var (searchClient, _) = await GetAuthenticatedClientAsync($"search16_renter_{Guid.NewGuid()}@test.com", Roles.Customer);
        var searchRes = await searchClient.GetAsync("/api/machinery?search=cultivator");
        var paged = await searchRes.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.Contains(paged.Items, m => m.Name.Contains("Super Cultivator 3000"));
    }

    [Fact]
    public async Task Test17_Search_By_Brand_Works()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"search17_owner_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Power Seed Drill", "Seed Drill", "Fieldking", "SD2", 2022, "Desc", 1200, 300, false, false, false, 0, null, null, null, "Anand", "Anand", "Gujarat", "388001"));

        var (searchClient, _) = await GetAuthenticatedClientAsync($"search17_renter_{Guid.NewGuid()}@test.com", Roles.Customer);
        var searchRes = await searchClient.GetAsync("/api/machinery?brand=fieldking");
        var paged = await searchRes.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.Contains(paged.Items, m => m.Brand == "Fieldking");
    }

    [Fact]
    public async Task Test18_Search_By_Model_Works()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"search18_owner_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Heavy Duty Tractor", "Tractor", "Eicher", "XTR-99", 2023, "Desc", 1800, 400, false, false, false, 0, null, null, null, "Surat", "Surat", "Gujarat", "395001"));

        var (searchClient, _) = await GetAuthenticatedClientAsync($"search18_renter_{Guid.NewGuid()}@test.com", Roles.Customer);
        var searchRes = await searchClient.GetAsync("/api/machinery?search=xtr-99");
        var paged = await searchRes.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.Contains(paged.Items, m => m.Model == "XTR-99");
    }

    [Fact]
    public async Task Test19_Category_Filter_Works()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"cat19_owner_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Rotavator Pro", "Rotavator", "Shaktiman", "R1", 2022, "Desc", 1100, 300, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));

        var (searchClient, _) = await GetAuthenticatedClientAsync($"cat19_renter_{Guid.NewGuid()}@test.com", Roles.Customer);
        var res = await searchClient.GetAsync("/api/machinery?category=Rotavator");
        var paged = await res.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.All(paged.Items, m => Assert.Equal("Rotavator", m.Category));
    }

    [Fact]
    public async Task Test20_Price_Filter_Works()
    {
        var (client, _) = await GetAuthenticatedClientAsync($"price20_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await client.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Budget Sprayer", "Sprayer", "Aspee", "B1", 2022, "Desc", 500, 100, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        await client.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Luxury JCB", "JCB", "JCB", "L1", 2023, "Desc", 5000, 1500, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));

        var res = await client.GetAsync("/api/machinery?minRentPerDay=400&maxRentPerDay=1000");
        var paged = await res.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.All(paged.Items, m => Assert.True(m.DailyRent >= 400 && m.DailyRent <= 1000));
    }

    [Fact]
    public async Task Test21_Driver_Filter_Works()
    {
        var (client, _) = await GetAuthenticatedClientAsync($"driver21_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await client.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("With Driver Tractor", "Tractor", "Sonalika", "D1", 2022, "Desc", 2000, 500, false, false, true, 500, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));

        var res = await client.GetAsync("/api/machinery?driverAvailable=true");
        var paged = await res.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.All(paged.Items, m => Assert.True(m.DriverAvailable));
    }

    [Fact]
    public async Task Test22_Location_Filter_Works()
    {
        var (client, _) = await GetAuthenticatedClientAsync($"loc22_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await client.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Nadiad Harvester", "Harvester", "Preet", "H1", 2022, "Desc", 3500, 800, false, false, false, 0, null, "Nadiad", "Gujarat", "387001"));

        var res = await client.GetAsync("/api/machinery?city=Nadiad");
        var paged = await res.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.All(paged.Items, m => Assert.Equal("Nadiad", m.City));
    }

    [Fact]
    public async Task Test23_Availability_Date_Filter_Works()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner23_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Busy Tractor", "Tractor", "Mahindra", "575", 2022, "Desc", 1800, 500, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter23_{Guid.NewGuid()}@test.com", Roles.Customer);
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var end = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));
        await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(start, end, false, "UPI"));

        // Search for overlapping dates (12 to 14 Aug)
        var res1 = await renter.GetAsync($"/api/machinery?startDate={start.AddDays(2):yyyy-MM-dd}&endDate={end.AddDays(-1):yyyy-MM-dd}");
        var paged1 = await res1.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);
        Assert.DoesNotContain(paged1!.Items, m => m.Id == mach.Id);

        // Search for non-overlapping dates (16 to 20 Aug)
        var res2 = await renter.GetAsync($"/api/machinery?startDate={end.AddDays(1):yyyy-MM-dd}&endDate={end.AddDays(5):yyyy-MM-dd}");
        var paged2 = await res2.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);
        Assert.Contains(paged2!.Items, m => m.Id == mach.Id);
    }

    [Fact]
    public async Task Test24_Multiple_Filters_Work_Together()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"multi24_owner_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest(Name: "Specific Tractor", Category: "Tractor", Brand: "John Deere", Model: "JD1", ManufacturingYear: 2022, Description: "Desc", DailyRent: 1600, SecurityDeposit: 400, IsDriverIncluded: false, IsFuelIncluded: false, DriverAvailable: true, DriverChargePerDay: 400, Location: "Morbi Road", City: "Morbi", State: "Gujarat", Pincode: "363641"));

        var (searchClient, _) = await GetAuthenticatedClientAsync($"multi24_renter_{Guid.NewGuid()}@test.com", Roles.Customer);
        var res = await searchClient.GetAsync("/api/machinery?search=specific&category=Tractor&minRentPerDay=1000&maxRentPerDay=2000&driverAvailable=true&city=Morbi");
        var paged = await res.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("Specific Tractor", paged.Items[0].Name);
    }

    [Fact]
    public async Task Test25_Sorting_Works()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"sort25_owner_{Guid.NewGuid()}@test.com", Roles.Farmer);
        await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Cheap Tool", "Plough", "P1", "M1", 2020, "Desc", 400, 100, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Expensive Tool", "Harvester", "H1", "M2", 2023, "Desc", 4500, 1000, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));

        var (searchClient, _) = await GetAuthenticatedClientAsync($"sort25_renter_{Guid.NewGuid()}@test.com", Roles.Customer);
        var resAsc = await searchClient.GetAsync("/api/machinery?sortBy=priceAsc");
        var pagedAsc = await resAsc.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);
        Assert.True(pagedAsc!.Items.First().DailyRent <= pagedAsc.Items.Last().DailyRent);

        var resDesc = await searchClient.GetAsync("/api/machinery?sortBy=priceDesc");
        var pagedDesc = await resDesc.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);
        Assert.True(pagedDesc!.Items.First().DailyRent >= pagedDesc.Items.Last().DailyRent);
    }

    [Fact]
    public async Task Test26_Pagination_Works()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"page26_owner_{Guid.NewGuid()}@test.com", Roles.Farmer);
        for (int i = 1; i <= 5; i++)
        {
            await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest($"Page Tool {i}", "Other", "B1", "M1", 2022, "Desc", 1000 + i * 100, 200, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        }

        var (searchClient, _) = await GetAuthenticatedClientAsync($"page26_renter_{Guid.NewGuid()}@test.com", Roles.Customer);
        var res = await searchClient.GetAsync("/api/machinery?page=1&pageSize=2");
        var paged = await res.Content.ReadFromJsonAsync<PagedMachineryResponse>(JsonOptions);

        Assert.NotNull(paged);
        Assert.Equal(1, paged.Page);
        Assert.Equal(2, paged.PageSize);
        Assert.Equal(2, paged.Items.Count);
        Assert.True(paged.TotalCount >= 5);
    }

    [Fact]
    public async Task Test27_Overlapping_Rentals_Rejected()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner27_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Overlap Tractor", "Tractor", "Eicher", "E1", 2022, "Desc", 1500, 400, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20));
        var end = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25));

        var (renterA, _) = await GetAuthenticatedClientAsync($"renterA27_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes1 = await renterA.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(start, end, false, "UPI"));
        Assert.Equal(HttpStatusCode.Created, bookRes1.StatusCode);

        var (renterB, _) = await GetAuthenticatedClientAsync($"renterB27_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes2 = await renterB.PostAsJsonAsync($"/api/machinery/{mach.Id}/rentals", new BookRentalRequest(start.AddDays(2), end.AddDays(2), false, "UPI"));
        Assert.Equal(HttpStatusCode.Conflict, bookRes2.StatusCode);
    }

    [Fact]
    public async Task Test28_Concurrent_Rental_Handled_Safely()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner28_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Concurrent Tractor", "Tractor", "Sonalika", "S1", 2022, "Desc", 1600, 400, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var end = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35));

        var (renter1, _) = await GetAuthenticatedClientAsync($"renter1_28_{Guid.NewGuid()}@test.com", Roles.Customer);
        var (renter2, _) = await GetAuthenticatedClientAsync($"renter2_28_{Guid.NewGuid()}@test.com", Roles.Farmer);

        var res1 = await renter1.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(start, end, false, "UPI"));
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var res2 = await renter2.PostAsJsonAsync($"/api/machinery/{mach.Id}/rentals", new BookRentalRequest(start, end, false, "Card"));
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task Test29_Unauthorized_Machinery_Modification_Rejected()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner29_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Mod Protection", "Tractor", "Mahindra", "M1", 2022, "Desc", 1500, 400, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (otherUser, _) = await GetAuthenticatedClientAsync($"other29_{Guid.NewGuid()}@test.com", Roles.Customer);
        var updateRes = await otherUser.PutAsJsonAsync($"/api/my-machinery/{mach!.Id}", new UpdateMachineryRequest("Hacked Name", null, null, null, null, null, 1, null, null, null, null, null, null, null, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, updateRes.StatusCode); // OwnerUserId mismatch returns 404
    }

    [Fact]
    public async Task Test30_Unauthorized_Rental_Access_Rejected()
    {
        var (owner, _) = await GetAuthenticatedClientAsync($"owner30_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var createRes = await owner.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest("Secret Rental", "Tractor", "Mahindra", "M1", 2022, "Desc", 1500, 400, false, false, false, 0, null, null, null, "Rajkot", "Rajkot", "Gujarat", "360005"));
        var mach = await createRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var (renter, _) = await GetAuthenticatedClientAsync($"renter30_{Guid.NewGuid()}@test.com", Roles.Customer);
        var bookRes = await renter.PostAsJsonAsync($"/api/machinery/{mach!.Id}/rentals", new BookRentalRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), false, "UPI"));
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        var (unrelatedUser, _) = await GetAuthenticatedClientAsync($"unrelated30_{Guid.NewGuid()}@test.com", Roles.Farmer);
        var getRes = await unrelatedUser.GetAsync($"/api/rentals/{rental!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }
}
