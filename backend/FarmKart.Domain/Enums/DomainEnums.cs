namespace FarmKart.Domain.Enums;

public enum JobStatus
{
    Draft = 1,
    Open = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}

public enum ApplicationStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3,
    Withdrawn = 4
}

public enum AssignmentStatus
{
    Pending = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    HalfDay = 3,
    Leave = 4
}

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4,
    PartiallyPaid = 5
}

public enum PaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
    Upi = 3,
    Card = 4,
    Other = 5
}

public enum MachineryAvailabilityStatus
{
    Available = 1,
    Reserved = 2,
    Rented = 3,
    Maintenance = 4,
    Unavailable = 5
}

public enum RentalRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum RentalStatus
{
    Upcoming = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4,
    Disputed = 5
}

public enum DamageReportStatus
{
    Reported = 1,
    UnderReview = 2,
    Resolved = 3,
    Waived = 4
}

public enum CropStatus
{
    Planned = 1,
    Growing = 2,
    ReadyForHarvest = 3,
    Harvested = 4,
    Sold = 5,
    Archived = 6
}

public enum MeasurementUnit
{
    Kilogram = 1,
    Quintal = 2,
    Ton = 3,
    Piece = 4,
    Litre = 5,
    Acre = 6
}

public enum ListingType
{
    DirectSale = 1,
    Auction = 2
}

public enum ListingStatus
{
    Draft = 1,
    Active = 2,
    Paused = 3,
    SoldOut = 4,
    Closed = 5
}

public enum AuctionStatus
{
    Draft = 1,
    Scheduled = 2,
    Live = 3,
    Ended = 4,
    Cancelled = 5,
    Finalized = 6
}

public enum BidStatus
{
    Active = 1,
    Outbid = 2,
    Winning = 3,
    Cancelled = 4,
    Rejected = 5
}

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Packed = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6
}

public enum DeliveryStatus
{
    Pending = 1,
    Scheduled = 2,
    InTransit = 3,
    Delivered = 4,
    Failed = 5,
    Cancelled = 6
}

public enum DeliveryType
{
    Pickup = 1,
    HomeDelivery = 2
}

public enum NotificationType
{
    General = 1,
    Job = 2,
    Application = 3,
    Assignment = 4,
    MachineryRental = 5,
    CropListing = 6,
    Auction = 7,
    Order = 8,
    Payment = 9,
    Chat = 10,
    Review = 11
}

public enum ReviewEntityType
{
    Job = 1,
    WorkerAssignment = 2,
    MachineryRental = 3,
    Order = 4,
    Auction = 5
}

public enum ParticipantProfileType
{
    Farmer = 1,
    Worker = 2,
    Customer = 3
}
