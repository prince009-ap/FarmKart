using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

public sealed class FarmerProfile : ProfileBase
{
    public string? FarmName { get; set; }
    public decimal? FarmSize { get; set; }
    public FarmSizeUnit? FarmSizeUnit { get; set; }
    public string? FarmLocation { get; set; }

    public ICollection<Job> Jobs { get; set; } = [];
    public ICollection<WorkerPayment> WorkerPayments { get; set; } = [];
    public ICollection<Machinery> OwnedMachinery { get; set; } = [];
    public ICollection<MachineryRentalRequest> MachineryRentalRequestsAsRenter { get; set; } = [];
    public ICollection<MachineryRentalRequest> MachineryRentalRequestsAsOwner { get; set; } = [];
    public ICollection<MachineryRental> MachineryRentalsAsOwner { get; set; } = [];
    public ICollection<MachineryRental> MachineryRentalsAsRenter { get; set; } = [];
    public ICollection<Crop> Crops { get; set; } = [];
    public ICollection<CropListing> CropListings { get; set; } = [];
    public ICollection<Auction> Auctions { get; set; } = [];
}

public sealed class WorkerProfile : ProfileBase
{
    public int ExperienceYears { get; set; }
    public string? ExperienceDescription { get; set; }
    public decimal ExpectedDailyWage { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateOnly? AvailableFrom { get; set; }
    public string? AvailabilityNotes { get; set; }
    public string? PreferredWorkCategories { get; set; }
    public string? PreferredLocations { get; set; }
    public decimal MinimumDailyWage { get; set; }
    public string? PreferredWorkingHours { get; set; }
    public string? FoodPreference { get; set; }
    public string? AccommodationPreference { get; set; }
    public string VerificationStatus { get; set; } = "Not Verified";

    public ICollection<WorkerSkill> WorkerSkills { get; set; } = [];
    public ICollection<JobApplication> JobApplications { get; set; } = [];
    public ICollection<WorkerAssignment> WorkerAssignments { get; set; } = [];
    public ICollection<WorkerPayment> WorkerPayments { get; set; } = [];
}

public sealed class CustomerProfile : ProfileBase
{
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Bid> Bids { get; set; } = [];
    public ICollection<AuctionWinner> AuctionWins { get; set; } = [];
}
