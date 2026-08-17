using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.Abstractions.Machinery;
using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.Abstractions.Worker;
using FarmKart.Application.Abstractions.Persistence;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using FarmKart.Application.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace FarmKart.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=FarmKartDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<FarmKartDbContext>(options =>
    options.UseSqlServer(connectionString));

        services.AddScoped<IFarmKartDbContext>(provider => provider.GetRequiredService<FarmKartDbContext>());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFarmerProfileService, FarmerProfileService>();
        services.AddScoped<IFarmerJobService, FarmerJobService>();
        services.AddScoped<IFarmerApplicationService, FarmerApplicationService>();
        services.AddScoped<IFarmerAssignmentService, FarmerAssignmentService>();
        services.AddScoped<IFarmerAttendanceService, FarmerAttendanceService>();
        services.AddScoped<IFarmerCropService, FarmerCropService>();
        services.AddScoped<IFarmerCropStockService, FarmerCropStockService>();
        services.AddScoped<IFarmerAuctionService, FarmerAuctionService>();
        services.AddScoped<IWorkerJobService, WorkerJobService>();
        services.AddScoped<IWorkerAssignmentService, WorkerAssignmentService>();
        services.AddScoped<IWorkerAttendanceService, WorkerAttendanceService>();
        services.AddScoped<IWorkerProfileService, WorkerProfileService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IWorkerReviewService, WorkerReviewService>();
        services.AddScoped<IWorkerEarningsService, WorkerEarningsService>();
        services.AddScoped<IWorkerWorkHistoryService, WorkerWorkHistoryService>();
        services.AddScoped<IWorkerProfileCompletionService, WorkerProfileCompletionService>();
        services.AddScoped<FarmKart.Application.Abstractions.Customer.ICustomerAuctionService, CustomerAuctionService>();
        services.AddScoped<FarmKart.Application.Abstractions.Auctions.IAuctionFinalizationService, AuctionFinalizationService>();
        services.AddScoped<FarmKart.Application.Abstractions.Payments.IPaymentProvider, MockPaymentProvider>();
        services.AddScoped<FarmKart.Application.Abstractions.Customer.ICustomerPaymentService, CustomerPaymentService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderReviewService, OrderReviewService>();
        services.AddScoped<IWishlistService, WishlistService>();
        services.AddScoped<IPaymentOrderBackfillService, PaymentOrderBackfillService>();
        services.AddScoped<IMachineryService, MachineryService>();
        services.AddScoped<IMachineryRentalService, MachineryRentalService>();
        services.AddScoped<IMachineryReviewService, MachineryReviewService>();
        services.AddScoped<IFarmerAnalyticsService, FarmerAnalyticsService>();
        services.AddScoped<ICustomerAnalyticsService, CustomerAnalyticsService>();
        services.AddScoped<FarmKart.Application.Abstractions.Report.IReportService, ReportService>();
        services.AddScoped<FarmKart.Application.Abstractions.Dispute.IDisputeService, DisputeService>();
        services.AddHostedService<AuctionFinalizationBackgroundService>();

        // Register JWT options and services
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var settings = jwtOptions.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var cookieName = settings.CookieName ?? "FarmKartAuth";
                        if (context.Request.Cookies.TryGetValue(cookieName, out var token))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<FarmKartDbContext>();

        return services;
    }
}
