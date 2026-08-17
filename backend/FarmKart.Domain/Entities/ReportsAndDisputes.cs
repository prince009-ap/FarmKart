using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

/// <summary>
/// Domain entity representing a user report against inappropriate content or behavior (Auction, Machinery, Review, User).
/// </summary>
public sealed class UserReport : BaseEntity
{
    /// <summary>ApplicationUser.Id of the user submitting the report.</summary>
    public string ReporterUserId { get; set; } = string.Empty;

    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public string? ResolutionNote { get; set; }
}

/// <summary>
/// Domain entity representing a transaction dispute raised by a participant (Order, Payment, AuctionAllocation, MachineryRental).
/// </summary>
public sealed class UserDispute : BaseEntity
{
    /// <summary>ApplicationUser.Id of the user raising the dispute.</summary>
    public string RaisedByUserId { get; set; } = string.Empty;

    public DisputeEntityType RelatedEntityType { get; set; }
    public Guid RelatedEntityId { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public string? ResolutionNote { get; set; }
}
