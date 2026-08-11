using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

public sealed class MachineryCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Machinery> MachineryItems { get; set; } = [];
}

public sealed class Machinery : BaseEntity
{
    public Guid OwnerFarmerProfileId { get; set; }
    public FarmerProfile OwnerFarmerProfile { get; set; } = null!;
    public Guid MachineryCategoryId { get; set; }
    public MachineryCategory MachineryCategory { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public int? ManufacturingYear { get; set; }
    public string? Description { get; set; }
    public decimal DailyRent { get; set; }
    public decimal? WeeklyRent { get; set; }
    public decimal? MonthlyRent { get; set; }
    public decimal SecurityDeposit { get; set; }
    public bool IsDriverIncluded { get; set; }
    public bool IsFuelIncluded { get; set; }
    public MachineryAvailabilityStatus AvailabilityStatus { get; set; } = MachineryAvailabilityStatus.Available;
    public string Location { get; set; } = string.Empty;

    public ICollection<MachineryImage> Images { get; set; } = [];
    public ICollection<MachineryRentalRequest> RentalRequests { get; set; } = [];
    public ICollection<MachineryRental> Rentals { get; set; } = [];
}

public sealed class MachineryImage : BaseEntity
{
    public Guid MachineryId { get; set; }
    public Machinery Machinery { get; set; } = null!;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class MachineryRentalRequest : BaseEntity
{
    public Guid MachineryId { get; set; }
    public Machinery Machinery { get; set; } = null!;
    public Guid RenterFarmerProfileId { get; set; }
    public FarmerProfile RenterFarmerProfile { get; set; } = null!;
    public Guid OwnerFarmerProfileId { get; set; }
    public FarmerProfile OwnerFarmerProfile { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal SecurityDeposit { get; set; }
    public RentalRequestStatus RequestStatus { get; set; } = RentalRequestStatus.Pending;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }

    public MachineryRental? MachineryRental { get; set; }
}

public sealed class MachineryRental : BaseEntity
{
    public Guid MachineryId { get; set; }
    public Machinery Machinery { get; set; } = null!;
    public Guid OwnerFarmerProfileId { get; set; }
    public FarmerProfile OwnerFarmerProfile { get; set; } = null!;
    public Guid RenterFarmerProfileId { get; set; }
    public FarmerProfile RenterFarmerProfile { get; set; } = null!;
    public Guid? MachineryRentalRequestId { get; set; }
    public MachineryRentalRequest? MachineryRentalRequest { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal SecurityDeposit { get; set; }
    public RentalStatus RentalStatus { get; set; } = RentalStatus.Upcoming;
    public DateTime? ReturnedAtUtc { get; set; }

    public ICollection<MachineryDamageReport> DamageReports { get; set; } = [];
}

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
