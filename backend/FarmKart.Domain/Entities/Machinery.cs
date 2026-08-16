using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

/// <summary>
/// Machinery listing owned by any ApplicationUser (Farmer or Customer).
/// OwnerUserId stores the ApplicationUser.Id (Guid as string).
/// </summary>
public sealed class Machinery : BaseEntity
{
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public int? ManufacturingYear { get; set; }
    public string? Description { get; set; }
    public decimal DailyRent { get; set; }
    public decimal SecurityDeposit { get; set; }
    public bool IsDriverIncluded { get; set; }
    public bool IsFuelIncluded { get; set; }
    public MachineryAvailabilityStatus AvailabilityStatus { get; set; } = MachineryAvailabilityStatus.Available;
    public string Location { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MachineryImage> Images { get; set; } = [];
    public ICollection<MachineryRental> Rentals { get; set; } = [];
}

public sealed class MachineryImage : BaseEntity
{
    public Guid MachineryId { get; set; }
    public Machinery Machinery { get; set; } = null!;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// A confirmed rental booking. RenterUserId must differ from OwnerUserId.
/// Status lifecycle: Booked → Confirmed → ReadyForHandover → RentedOut → Returned → Completed (or Cancelled).
/// </summary>
public sealed class MachineryRental : BaseEntity
{
    public Guid MachineryId { get; set; }
    public Machinery Machinery { get; set; } = null!;
    public string OwnerUserId { get; set; } = string.Empty;
    public string RenterUserId { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int RentalDays { get; set; }
    public decimal RentPerDaySnapshot { get; set; }
    public decimal SecurityDepositSnapshot { get; set; }
    public decimal TotalRentAmount { get; set; }
    public decimal TotalPayableAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? PaymentTransactionRef { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public RentalStatus RentalStatus { get; set; } = RentalStatus.Booked;
    public DateTime? ReturnedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<MachineryDamageReport> DamageReports { get; set; } = [];
}


/// <summary>
/// Damage reports: stored in DB but Phase 8.4 API is out of scope.
/// </summary>
public sealed class MachineryDamageReport : BaseEntity
{
    public Guid MachineryRentalId { get; set; }
    public MachineryRental MachineryRental { get; set; } = null!;
    public string ReportedByUserId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DamageAmount { get; set; }
    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
    public DamageReportStatus Status { get; set; } = DamageReportStatus.Reported;

    public ICollection<MachineryDamageReportImage> Images { get; set; } = [];
}

public sealed class MachineryDamageReportImage : BaseEntity
{
    public Guid MachineryDamageReportId { get; set; }
    public MachineryDamageReport MachineryDamageReport { get; set; } = null!;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
