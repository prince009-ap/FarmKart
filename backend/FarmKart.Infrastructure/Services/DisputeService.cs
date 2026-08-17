using FarmKart.Application.Abstractions.Dispute;
using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class DisputeService : IDisputeService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public DisputeService(FarmKartDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<UserDisputeResponse> CreateDisputeAsync(Guid userId, CreateDisputeRequest request, CancellationToken cancellationToken = default)
    {
        var raisedByUserIdStr = userId.ToString();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Reason is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.");
        }

        string entityTitle = request.RelatedEntityType.ToString();
        string? oppositeUserIdStr = null;

        // 1. Authorization & Participation Validation
        switch (request.RelatedEntityType)
        {
            case DisputeEntityType.Order:
                // Check Order table OR AuctionOrder table
                var order = await _dbContext.Orders
                    .AsNoTracking()
                    .Include(o => o.CustomerProfile)
                    .FirstOrDefaultAsync(o => o.Id == request.RelatedEntityId, cancellationToken);

                if (order != null)
                {
                    bool isCustomer = order.CustomerProfile?.UserId == userId;

                    if (!isCustomer)
                    {
                        throw new UnauthorizedAccessException("You are not authorized to raise a dispute on this order.");
                    }

                    entityTitle = $"Order #{order.OrderNumber}";
                }
                else
                {
                    var auctionOrder = await _dbContext.AuctionOrders
                        .AsNoTracking()
                        .Include(o => o.CustomerProfile)
                        .Include(o => o.FarmerProfile)
                        .Include(o => o.Crop)
                        .FirstOrDefaultAsync(o => o.Id == request.RelatedEntityId, cancellationToken);

                    if (auctionOrder == null)
                    {
                        throw new KeyNotFoundException($"Order with ID '{request.RelatedEntityId}' was not found.");
                    }

                    bool isCustomer = auctionOrder.CustomerProfile?.UserId == userId;
                    bool isFarmer = auctionOrder.FarmerProfile?.UserId == userId;

                    if (!isCustomer && !isFarmer)
                    {
                        throw new UnauthorizedAccessException("You are not authorized to raise a dispute on this order.");
                    }

                    oppositeUserIdStr = isCustomer ? auctionOrder.FarmerProfile?.UserId.ToString() : auctionOrder.CustomerProfile?.UserId.ToString();
                    entityTitle = $"Auction Order: {auctionOrder.Crop?.CropName ?? "Crop"}";
                }
                break;

            case DisputeEntityType.MachineryRental:
                var rental = await _dbContext.MachineryRentals
                    .AsNoTracking()
                    .Include(r => r.Machinery)
                    .FirstOrDefaultAsync(r => r.Id == request.RelatedEntityId, cancellationToken);

                if (rental == null)
                {
                    throw new KeyNotFoundException($"Machinery rental with ID '{request.RelatedEntityId}' was not found.");
                }

                bool isRenter = rental.RenterUserId == raisedByUserIdStr;
                bool isOwner = rental.OwnerUserId == raisedByUserIdStr;

                if (!isRenter && !isOwner)
                {
                    throw new UnauthorizedAccessException("You are not authorized to raise a dispute on this machinery rental.");
                }

                oppositeUserIdStr = isRenter ? rental.OwnerUserId : rental.RenterUserId;
                entityTitle = $"Rental: {rental.Machinery?.Name ?? "Machinery"}";
                break;

            case DisputeEntityType.AuctionAllocation:
                var allocation = await _dbContext.AuctionAllocations
                    .AsNoTracking()
                    .Include(a => a.CustomerProfile)
                    .Include(a => a.Auction)
                        .ThenInclude(a => a.CropListing)
                            .ThenInclude(l => l.Crop)
                    .FirstOrDefaultAsync(a => a.Id == request.RelatedEntityId, cancellationToken);

                if (allocation == null)
                {
                    throw new KeyNotFoundException($"Auction allocation with ID '{request.RelatedEntityId}' was not found.");
                }

                bool isWinnerCustomer = allocation.CustomerProfile?.UserId == userId;
                var farmerProfile = await _dbContext.FarmerProfiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == allocation.Auction.FarmerProfileId, cancellationToken);
                bool isAuctionFarmer = farmerProfile?.UserId == userId;

                if (!isWinnerCustomer && !isAuctionFarmer)
                {
                    throw new UnauthorizedAccessException("You are not authorized to raise a dispute on this auction allocation.");
                }

                oppositeUserIdStr = isWinnerCustomer ? farmerProfile?.UserId.ToString() : allocation.CustomerProfile?.UserId.ToString();
                entityTitle = $"Allocation for {allocation.Auction?.CropListing?.Crop?.CropName ?? "Auction"}";
                break;

            case DisputeEntityType.Payment:
                var payment = await _dbContext.Payments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == request.RelatedEntityId, cancellationToken);

                if (payment == null)
                {
                    throw new KeyNotFoundException($"Payment with ID '{request.RelatedEntityId}' was not found.");
                }

                entityTitle = $"Payment #{payment.Id.ToString()[..8]}";
                break;

            default:
                throw new ArgumentException("Invalid dispute entity type.");
        }

        // 2. Prevent duplicate OPEN disputes for the same entity and reason by the same user
        var duplicateExists = await _dbContext.Disputes
            .AsNoTracking()
            .AnyAsync(d => d.RaisedByUserId == raisedByUserIdStr &&
                           d.RelatedEntityType == request.RelatedEntityType &&
                           d.RelatedEntityId == request.RelatedEntityId &&
                           d.Reason == request.Reason.Trim() &&
                           (d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview), cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("An open dispute for this item with the same reason is already active.");
        }

        var dispute = new UserDispute
        {
            RaisedByUserId = raisedByUserIdStr,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            Reason = request.Reason.Trim(),
            Description = request.Description.Trim(),
            Status = DisputeStatus.Open
        };

        _dbContext.Disputes.Add(dispute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 3. Dispatch notifications
        // Reporter Notification
        await _notificationService.CreateNotificationAsync(
            recipientUserId: raisedByUserIdStr,
            title: "Dispute Submitted",
            message: $"Your dispute regarding {entityTitle} has been submitted.",
            notificationType: NotificationType.ReportDispute,
            relatedEntityId: dispute.Id,
            actionUrl: $"/disputes/{dispute.Id}",
            cancellationToken: cancellationToken);

        // Opposite Party Notification
        if (!string.IsNullOrEmpty(oppositeUserIdStr))
        {
            await _notificationService.CreateNotificationAsync(
                recipientUserId: oppositeUserIdStr,
                title: "Dispute Raised",
                message: $"A dispute has been raised regarding {entityTitle} (Reason: {dispute.Reason}).",
                notificationType: NotificationType.ReportDispute,
                relatedEntityId: dispute.Id,
                actionUrl: $"/disputes/{dispute.Id}",
                cancellationToken: cancellationToken);
        }

        return MapToResponse(dispute, entityTitle);
    }

    public async Task<PagedDisputeResponse> GetUserDisputesAsync(Guid userId, DisputeQueryRequest request, CancellationToken cancellationToken = default)
    {
        var userIdStr = userId.ToString();
        var query = _dbContext.Disputes
            .AsNoTracking()
            .Where(d => d.RaisedByUserId == userIdStr);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<DisputeStatus>(request.Status.Trim(), true, out var statusEnum))
        {
            query = query.Where(d => d.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.RelatedEntityType) && Enum.TryParse<DisputeEntityType>(request.RelatedEntityType.Trim(), true, out var entityTypeEnum))
        {
            query = query.Where(d => d.RelatedEntityType == entityTypeEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(d => d.Reason.ToLower().Contains(search) || d.Description.ToLower().Contains(search));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        int pageSize = Math.Max(1, Math.Min(100, request.PageSize));
        int page = Math.Max(1, request.Page);
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);

        var disputes = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserDisputeResponse>();
        foreach (var d in disputes)
        {
            var title = await ResolveEntityTitleAsync(d.RelatedEntityType, d.RelatedEntityId, cancellationToken);
            items.Add(MapToResponse(d, title));
        }

        return new PagedDisputeResponse(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }

    public async Task<UserDisputeResponse?> GetDisputeByIdAsync(Guid userId, Guid disputeId, CancellationToken cancellationToken = default)
    {
        var userIdStr = userId.ToString();

        var dispute = await _dbContext.Disputes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

        if (dispute == null)
        {
            return null;
        }

        // Verify authorization: User must be reporter OR opposite participant
        bool isReporter = dispute.RaisedByUserId == userIdStr;
        bool isParticipant = false;

        if (!isReporter)
        {
            isParticipant = await VerifyParticipantAccessAsync(dispute.RelatedEntityType, dispute.RelatedEntityId, userId, cancellationToken);
        }

        if (!isReporter && !isParticipant)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this dispute.");
        }

        var title = await ResolveEntityTitleAsync(dispute.RelatedEntityType, dispute.RelatedEntityId, cancellationToken);
        return MapToResponse(dispute, title);
    }

    public async Task<UserDisputeResponse> CloseDisputeAsync(Guid userId, Guid disputeId, string? resolutionNote = null, CancellationToken cancellationToken = default)
    {
        var userIdStr = userId.ToString();
        var dispute = await _dbContext.Disputes
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

        if (dispute == null)
        {
            throw new KeyNotFoundException($"Dispute with ID '{disputeId}' was not found.");
        }

        bool isReporter = dispute.RaisedByUserId == userIdStr;
        bool isParticipant = await VerifyParticipantAccessAsync(dispute.RelatedEntityType, dispute.RelatedEntityId, userId, cancellationToken);

        if (!isReporter && !isParticipant)
        {
            throw new UnauthorizedAccessException("You are not authorized to update this dispute.");
        }

        dispute.Status = DisputeStatus.Closed;
        dispute.ResolutionNote = string.IsNullOrWhiteSpace(resolutionNote)
            ? $"Dispute closed by {(isReporter ? "reporter" : "participant")}."
            : resolutionNote.Trim();
        dispute.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var title = await ResolveEntityTitleAsync(dispute.RelatedEntityType, dispute.RelatedEntityId, cancellationToken);

        // Notify reporter
        await _notificationService.CreateNotificationAsync(
            recipientUserId: dispute.RaisedByUserId,
            title: "Dispute Closed",
            message: $"Your dispute regarding {title} has been closed.",
            notificationType: NotificationType.ReportDispute,
            relatedEntityId: dispute.Id,
            actionUrl: $"/disputes/{dispute.Id}",
            cancellationToken: cancellationToken);

        return MapToResponse(dispute, title);
    }

    private async Task<bool> VerifyParticipantAccessAsync(DisputeEntityType entityType, Guid entityId, Guid userId, CancellationToken cancellationToken)
    {
        var userIdStr = userId.ToString();
        return entityType switch
        {
            DisputeEntityType.Order => await _dbContext.Orders.AsNoTracking().Include(o => o.CustomerProfile).AnyAsync(o => o.Id == entityId && o.CustomerProfile.UserId == userId, cancellationToken) ||
                                       await _dbContext.AuctionOrders.AsNoTracking().Include(o => o.CustomerProfile).Include(o => o.FarmerProfile).AnyAsync(o => o.Id == entityId && (o.CustomerProfile.UserId == userId || o.FarmerProfile.UserId == userId), cancellationToken),

            DisputeEntityType.MachineryRental => await _dbContext.MachineryRentals.AsNoTracking().AnyAsync(r => r.Id == entityId && (r.RenterUserId == userIdStr || r.OwnerUserId == userIdStr), cancellationToken),

            DisputeEntityType.AuctionAllocation => await _dbContext.AuctionAllocations.AsNoTracking().Include(a => a.CustomerProfile).AnyAsync(a => a.Id == entityId && a.CustomerProfile.UserId == userId, cancellationToken),

            _ => false
        };
    }

    private async Task<string> ResolveEntityTitleAsync(DisputeEntityType entityType, Guid entityId, CancellationToken cancellationToken)
    {
        return entityType switch
        {
            DisputeEntityType.Order => await _dbContext.Orders.AsNoTracking().Where(o => o.Id == entityId).Select(o => $"Order #{o.OrderNumber}").FirstOrDefaultAsync(cancellationToken) ??
                                       await _dbContext.AuctionOrders.AsNoTracking().Include(o => o.Crop).Where(o => o.Id == entityId).Select(o => $"Auction Order: {o.Crop.CropName}").FirstOrDefaultAsync(cancellationToken) ?? "Order",

            DisputeEntityType.MachineryRental => await _dbContext.MachineryRentals.AsNoTracking().Include(r => r.Machinery).Where(r => r.Id == entityId).Select(r => $"Rental: {r.Machinery.Name}").FirstOrDefaultAsync(cancellationToken) ?? "Machinery Rental",

            DisputeEntityType.AuctionAllocation => await _dbContext.AuctionAllocations.AsNoTracking().Include(a => a.Auction).ThenInclude(a => a.CropListing).ThenInclude(l => l.Crop).Where(a => a.Id == entityId).Select(a => $"Allocation for {a.Auction.CropListing.Crop.CropName}").FirstOrDefaultAsync(cancellationToken) ?? "Auction Allocation",

            DisputeEntityType.Payment => $"Payment #{entityId.ToString()[..8]}",

            _ => entityType.ToString()
        };
    }

    private static UserDisputeResponse MapToResponse(UserDispute d, string entityTitle)
    {
        var timeline = new List<DisputeTimelineItemDto>
        {
            new("Open", "Dispute submitted by user.", d.CreatedAtUtc)
        };

        if (d.Status == DisputeStatus.UnderReview)
        {
            timeline.Add(new("Under Review", "Dispute is currently under review.", d.UpdatedAtUtc));
        }
        else if (d.Status == DisputeStatus.Resolved)
        {
            timeline.Add(new("Resolved", d.ResolutionNote ?? "Dispute resolved.", d.UpdatedAtUtc));
        }
        else if (d.Status == DisputeStatus.Rejected)
        {
            timeline.Add(new("Rejected", d.ResolutionNote ?? "Dispute rejected.", d.UpdatedAtUtc));
        }
        else if (d.Status == DisputeStatus.Closed)
        {
            timeline.Add(new("Closed", d.ResolutionNote ?? "Dispute closed.", d.UpdatedAtUtc));
        }

        return new UserDisputeResponse(
            Id: d.Id,
            RaisedByUserId: d.RaisedByUserId,
            RelatedEntityType: d.RelatedEntityType,
            RelatedEntityId: d.RelatedEntityId,
            EntityTitle: entityTitle,
            Reason: d.Reason,
            Description: d.Description,
            Status: d.Status,
            ResolutionNote: d.ResolutionNote,
            Timeline: timeline,
            CreatedAtUtc: d.CreatedAtUtc,
            UpdatedAtUtc: d.UpdatedAtUtc
        );
    }
}
