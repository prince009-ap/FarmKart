using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.Abstractions.Report;
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

public sealed class ReportService : IReportService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public ReportService(FarmKartDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<UserReportResponse> CreateReportAsync(Guid userId, CreateReportRequest request, CancellationToken cancellationToken = default)
    {
        var reporterUserIdStr = userId.ToString();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Reason is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.");
        }

        string targetTitle = request.TargetType.ToString();

        // 1. Target Validation & Title Resolution
        switch (request.TargetType)
        {
            case ReportTargetType.Auction:
                var auction = await _dbContext.Auctions
                    .AsNoTracking()
                    .Include(a => a.CropListing)
                        .ThenInclude(l => l.Crop)
                    .FirstOrDefaultAsync(a => a.Id == request.TargetId, cancellationToken);

                if (auction == null)
                {
                    throw new KeyNotFoundException($"Auction with ID '{request.TargetId}' was not found.");
                }

                targetTitle = $"Auction: {auction.CropListing?.Crop?.CropName ?? "Crop Listing"}";
                break;

            case ReportTargetType.Machinery:
                var machinery = await _dbContext.Machinery
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == request.TargetId, cancellationToken);

                if (machinery == null)
                {
                    throw new KeyNotFoundException($"Machinery with ID '{request.TargetId}' was not found.");
                }

                if (machinery.OwnerUserId == reporterUserIdStr)
                {
                    throw new InvalidOperationException("You cannot submit a public report against your own machinery.");
                }

                targetTitle = $"Machinery: {machinery.Name}";
                break;

            case ReportTargetType.Review:
                var review = await _dbContext.Reviews
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == request.TargetId, cancellationToken);

                if (review == null)
                {
                    throw new KeyNotFoundException($"Review with ID '{request.TargetId}' was not found.");
                }

                targetTitle = $"Review: #{review.Id.ToString()[..8]}";
                break;

            case ReportTargetType.User:
                var targetUser = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.TargetId, cancellationToken);

                if (targetUser == null)
                {
                    throw new KeyNotFoundException($"User with ID '{request.TargetId}' was not found.");
                }

                targetTitle = $"User: {targetUser.UserName ?? targetUser.Email ?? "Profile"}";
                break;

            default:
                throw new ArgumentException("Invalid report target type.");
        }

        // 2. Prevent duplicate open reports for the same target by the same user
        var duplicateExists = await _dbContext.Reports
            .AsNoTracking()
            .AnyAsync(r => r.ReporterUserId == reporterUserIdStr &&
                           r.TargetType == request.TargetType &&
                           r.TargetId == request.TargetId &&
                           (r.Status == ReportStatus.Open || r.Status == ReportStatus.UnderReview), cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("An open report for this item has already been submitted.");
        }

        var report = new UserReport
        {
            ReporterUserId = reporterUserIdStr,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Reason = request.Reason.Trim(),
            Description = request.Description.Trim(),
            Status = ReportStatus.Open
        };

        _dbContext.Reports.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 3. Dispatch confirmation notification to reporter
        await _notificationService.CreateNotificationAsync(
            recipientUserId: reporterUserIdStr,
            title: "Report Submitted",
            message: $"Your report for {targetTitle} has been submitted and is under review.",
            notificationType: NotificationType.ReportDispute,
            relatedEntityId: report.Id,
            actionUrl: "/my-reports",
            cancellationToken: cancellationToken);

        return MapToResponse(report, targetTitle);
    }

    public async Task<PagedReportResponse> GetUserReportsAsync(Guid userId, ReportQueryRequest request, CancellationToken cancellationToken = default)
    {
        var reporterUserIdStr = userId.ToString();
        var query = _dbContext.Reports
            .AsNoTracking()
            .Where(r => r.ReporterUserId == reporterUserIdStr);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ReportStatus>(request.Status.Trim(), true, out var statusEnum))
        {
            query = query.Where(r => r.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetType) && Enum.TryParse<ReportTargetType>(request.TargetType.Trim(), true, out var targetEnum))
        {
            query = query.Where(r => r.TargetType == targetEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(r => r.Reason.ToLower().Contains(search) || r.Description.ToLower().Contains(search));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        int pageSize = Math.Max(1, Math.Min(100, request.PageSize));
        int page = Math.Max(1, request.Page);
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);

        var reports = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserReportResponse>();
        foreach (var r in reports)
        {
            var title = await ResolveTargetTitleAsync(r.TargetType, r.TargetId, cancellationToken);
            items.Add(MapToResponse(r, title));
        }

        return new PagedReportResponse(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }

    public async Task<UserReportResponse?> GetReportByIdAsync(Guid userId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var reporterUserIdStr = userId.ToString();
        var report = await _dbContext.Reports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId && r.ReporterUserId == reporterUserIdStr, cancellationToken);

        if (report == null)
        {
            return null;
        }

        var title = await ResolveTargetTitleAsync(report.TargetType, report.TargetId, cancellationToken);
        return MapToResponse(report, title);
    }

    private async Task<string> ResolveTargetTitleAsync(ReportTargetType targetType, Guid targetId, CancellationToken cancellationToken)
    {
        return targetType switch
        {
            ReportTargetType.Auction => await _dbContext.Auctions.AsNoTracking().Include(a => a.CropListing).ThenInclude(l => l.Crop).Where(a => a.Id == targetId).Select(a => $"Auction: {a.CropListing.Crop.CropName}").FirstOrDefaultAsync(cancellationToken) ?? "Auction",
            ReportTargetType.Machinery => await _dbContext.Machinery.AsNoTracking().Where(m => m.Id == targetId).Select(m => $"Machinery: {m.Name}").FirstOrDefaultAsync(cancellationToken) ?? "Machinery",
            ReportTargetType.Review => $"Review: #{targetId.ToString()[..8]}",
            ReportTargetType.User => "User Profile",
            _ => targetType.ToString()
        };
    }

    private static UserReportResponse MapToResponse(UserReport r, string targetTitle) => new(
        Id: r.Id,
        ReporterUserId: r.ReporterUserId,
        TargetType: r.TargetType,
        TargetId: r.TargetId,
        TargetTitle: targetTitle,
        Reason: r.Reason,
        Description: r.Description,
        Status: r.Status,
        ResolutionNote: r.ResolutionNote,
        CreatedAtUtc: r.CreatedAtUtc,
        UpdatedAtUtc: r.UpdatedAtUtc
    );
}
