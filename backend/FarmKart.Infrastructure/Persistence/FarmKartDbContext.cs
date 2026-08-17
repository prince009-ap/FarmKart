using FarmKart.Application.Abstractions.Persistence;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Persistence;

public sealed class FarmKartDbContext(DbContextOptions<FarmKartDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IFarmKartDbContext
{
    public DbSet<FarmerProfile> FarmerProfiles => Set<FarmerProfile>();
    public DbSet<WorkerProfile> WorkerProfiles => Set<WorkerProfile>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<WorkerSkill> WorkerSkills => Set<WorkerSkill>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<WorkerAssignment> WorkerAssignments => Set<WorkerAssignment>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<WorkerPayment> WorkerPayments => Set<WorkerPayment>();
    public DbSet<Machinery> Machinery => Set<Machinery>();
    public DbSet<MachineryImage> MachineryImages => Set<MachineryImage>();
    public DbSet<MachineryRental> MachineryRentals => Set<MachineryRental>();
    public DbSet<MachineryDamageReport> MachineryDamageReports => Set<MachineryDamageReport>();
    public DbSet<MachineryDamageReportImage> MachineryDamageReportImages => Set<MachineryDamageReportImage>();
    public DbSet<Crop> Crops => Set<Crop>();
    public DbSet<CropListing> CropListings => Set<CropListing>();
    public DbSet<CropImage> CropImages => Set<CropImage>();
    public DbSet<CropStockTransaction> CropStockTransactions => Set<CropStockTransaction>();
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<AuctionAllocation> AuctionAllocations => Set<AuctionAllocation>();
    public DbSet<AuctionWinner> AuctionWinners => Set<AuctionWinner>();
    public DbSet<AuctionPayment> AuctionPayments => Set<AuctionPayment>();
    public DbSet<AuctionOrder> AuctionOrders => Set<AuctionOrder>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<OrderSettlement> OrderSettlements => Set<OrderSettlement>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<UserReport> Reports => Set<UserReport>();
    public DbSet<UserDispute> Disputes => Set<UserDispute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FarmKartDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserPreference>(builder =>
        {
            builder.HasIndex(up => up.UserId).IsUnique();
        });

        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }
    }

    public override int SaveChanges()
    {
        UpdateAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditTimestamps()
    {
        var entries = ChangeTracker
            .Entries<BaseEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAtUtc = DateTime.UtcNow;

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
        }
    }
}
