using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Helpers;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class CustomerAnalyticsService(FarmKartDbContext db) : ICustomerAnalyticsService
{
    private readonly FarmKartDbContext _db = db;

    public async Task<CustomerAnalyticsOverviewResponse> GetCustomerAnalyticsAsync(
        string customerUserId,
        AnalyticsDateRangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(customerUserId, out var custGuid))
        {
            throw new ArgumentException("Invalid customer user ID format", nameof(customerUserId));
        }

        var customer = await _db.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == custGuid, cancellationToken);

        var (fromDateUtc, toDateUtc, dateLabel) = AnalyticsDateHelper.CalculateDateRange(request);

        if (customer == null)
        {
            return CreateEmptyResponse(customerUserId, dateLabel, fromDateUtc, toDateUtc);
        }

        var customerProfileId = customer.Id;

        // 1. Bids & Auction Participation
        var bidsQuery = _db.Bids
            .AsNoTracking()
            .Include(b => b.Auction)
            .Where(b => b.CustomerProfileId == customerProfileId && b.BidTimeUtc >= fromDateUtc && b.BidTimeUtc <= toDateUtc);

        var customerBids = await bidsQuery.ToListAsync(cancellationToken);

        int totalBidsPlaced = customerBids.Count;
        int totalAuctionsParticipated = customerBids.Select(b => b.AuctionId).Distinct().Count();
        int liveBidsCount = customerBids.Count(b => b.Auction != null && b.Auction.AuctionStatus == AuctionStatus.Live);

        // Winning Allocations (Per Auction basis)
        var allocationsQuery = _db.AuctionAllocations
            .AsNoTracking()
            .Where(al => al.CustomerProfileId == customerProfileId && al.FinalizedAtUtc >= fromDateUtc && al.FinalizedAtUtc <= toDateUtc);

        var allocations = await allocationsQuery.ToListAsync(cancellationToken);
        int winningAuctionsCount = allocations
            .Where(al => al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon)
            .Select(al => al.AuctionId)
            .Distinct()
            .Count();

        double winningRatePercentage = totalAuctionsParticipated > 0
            ? Math.Min(100.0, Math.Round((double)winningAuctionsCount / totalAuctionsParticipated * 100.0, 1))
            : 0.0;

        // 2. Crop Orders & Spending
        var ordersQuery = _db.AuctionOrders
            .AsNoTracking()
            .Include(o => o.Crop)
            .Where(o => o.CustomerProfileId == customerProfileId && o.CreatedAtUtc >= fromDateUtc && o.CreatedAtUtc <= toDateUtc);

        var orders = await ordersQuery.ToListAsync(cancellationToken);

        decimal totalQuantityPurchasedKg = orders.Sum(o => o.AllocatedQuantityKg);
        decimal totalQuantityPurchasedMan = Math.Round(totalQuantityPurchasedKg / 20m, 2);

        int totalCropOrders = orders.Count;
        int completedOrdersCount = orders.Count(o => o.Status is OrderStatus.Completed or OrderStatus.Delivered);
        int activeOrdersCount = orders.Count(o => o.Status is OrderStatus.Confirmed or OrderStatus.ReadyForPickup or OrderStatus.Dispatched or OrderStatus.PickedUp);
        int cancelledOrdersCount = orders.Count(o => o.Status == OrderStatus.Cancelled);
        int pendingOrdersCount = orders.Count(o => o.Status == OrderStatus.Pending);

        var validOrders = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        decimal totalCropSpending = validOrders.Sum(o => o.TotalAmount);
        decimal averageOrderValue = validOrders.Count > 0 ? Math.Round(totalCropSpending / validOrders.Count, 2) : 0m;
        decimal highestOrderValue = validOrders.Count > 0 ? validOrders.Max(o => o.TotalAmount) : 0m;

        // 3. Machinery Rentals (Customer as RENTER)
        var customerRentals = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery)
            .Where(r => r.RenterUserId == customerUserId && r.CreatedAtUtc >= fromDateUtc && r.CreatedAtUtc <= toDateUtc)
            .ToListAsync(cancellationToken);

        int totalMachineryRentals = customerRentals.Count;
        int upcomingRentalsCount = customerRentals.Count(r => r.RentalStatus is RentalStatus.Booked or RentalStatus.Confirmed);
        int activeRentalsCount = customerRentals.Count(r => r.RentalStatus is RentalStatus.ReadyForHandover or RentalStatus.RentedOut);
        int completedRentalsCount = customerRentals.Count(r => r.RentalStatus is RentalStatus.Returned or RentalStatus.Completed);
        int cancelledRentalsCount = customerRentals.Count(r => r.RentalStatus == RentalStatus.Cancelled);

        var validRentals = customerRentals.Where(r => r.RentalStatus != RentalStatus.Cancelled).ToList();
        decimal totalMachineryRentalSpending = validRentals.Sum(r => r.TotalPayableAmount);
        double avgRentalDurationDays = validRentals.Count > 0 ? Math.Round(validRentals.Average(r => r.RentalDays), 1) : 0.0;

        int rentalsWithDriverCount = customerRentals.Count(r => r.DriverRequired);
        int rentalsWithoutDriverCount = customerRentals.Count(r => !r.DriverRequired);
        decimal driverSpending = validRentals.Where(r => r.DriverRequired).Sum(r => r.DriverAmount);

        // 4. Bi-directional Machinery (Customer as OWNER)
        var customerOwnedMachinery = await _db.Machinery
            .AsNoTracking()
            .Where(m => m.OwnerUserId == customerUserId && m.IsActive)
            .ToListAsync(cancellationToken);

        int machineryOwnedCount = customerOwnedMachinery.Count;

        var customerOwnedRentals = await _db.MachineryRentals
            .AsNoTracking()
            .Where(r => r.OwnerUserId == customerUserId && r.CreatedAtUtc >= fromDateUtc && r.CreatedAtUtc <= toDateUtc)
            .ToListAsync(cancellationToken);

        decimal machineryRentalIncome = customerOwnedRentals
            .Where(r => r.RentalStatus != RentalStatus.Cancelled)
            .Sum(r => r.TotalPayableAmount);

        // 5. Review Analytics (Written by Customer)
        var reviewsWritten = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewerUserId == customerUserId && r.CreatedAtUtc >= fromDateUtc && r.CreatedAtUtc <= toDateUtc)
            .ToListAsync(cancellationToken);

        int totalReviewsWritten = reviewsWritten.Count;
        int cropReviewsCount = reviewsWritten.Count(r => r.RelatedEntityType == ReviewEntityType.Order);
        int machineryReviewsCount = reviewsWritten.Count(r => r.RelatedEntityType == ReviewEntityType.MachineryRental);
        double avgRatingGiven = totalReviewsWritten > 0 ? Math.Round(reviewsWritten.Average(r => r.Rating), 1) : 0.0;
        var givenRatingDist = new RatingDistributionDto(
            FiveStar: reviewsWritten.Count(r => r.Rating == 5),
            FourStar: reviewsWritten.Count(r => r.Rating == 4),
            ThreeStar: reviewsWritten.Count(r => r.Rating == 3),
            TwoStar: reviewsWritten.Count(r => r.Rating == 2),
            OneStar: reviewsWritten.Count(r => r.Rating == 1)
        );

        // 6. Wishlist Analytics
        var wishlistItems = await _db.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == customerUserId)
            .ToListAsync(cancellationToken);

        int wishlistCount = wishlistItems.Count;
        int cropWishlistCount = wishlistItems.Count(w => w.ItemType == WishlistItemType.Crop);
        int auctionWishlistCount = wishlistItems.Count(w => w.ItemType == WishlistItemType.Auction);

        // 7. Time-Series Charts (Grouping by Day across full date range)
        var spendingDataMap = validOrders
            .GroupBy(o => o.CreatedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));

        var biddingDataMap = customerBids
            .GroupBy(b => b.BidTimeUtc.Date)
            .ToDictionary(g => g.Key, g => (decimal)g.Count());

        var spendingPoints = GenerateDailyPoints(fromDateUtc, toDateUtc, spendingDataMap);
        var biddingPoints = GenerateDailyPoints(fromDateUtc, toDateUtc, biddingDataMap);

        var spendingChart = new TimeSeriesChartDto("Spending Over Time", "Daily", spendingPoints);
        var biddingChart = new TimeSeriesChartDto("Bidding Activity Over Time", "Daily", biddingPoints);

        // 8. Top Rankings & Tables
        var topPurchasedCrops = validOrders
            .Where(o => o.Crop != null)
            .GroupBy(o => o.CropId)
            .Select(g =>
            {
                var crop = g.First().Crop;
                decimal purchasedKg = g.Sum(o => o.AllocatedQuantityKg);
                decimal totalSpend = g.Sum(o => o.TotalAmount);

                return new CustomerTopPurchasedCropResponse(
                    CropId: g.Key,
                    CropName: crop?.CropName ?? "Crop",
                    CropType: crop?.CropType ?? "N/A",
                    TotalPurchasedKg: purchasedKg,
                    TotalPurchasedMan: Math.Round(purchasedKg / 20m, 2),
                    TotalSpending: totalSpend,
                    OrdersCount: g.Count()
                );
            })
            .OrderByDescending(c => c.TotalPurchasedKg)
            .Take(5)
            .ToList();

        var rentalHistory = customerRentals
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(10)
            .Select(r => new CustomerMachineryRentalHistoryItemResponse(
                RentalId: r.Id,
                MachineryId: r.MachineryId,
                MachineryName: r.Machinery?.Name ?? "Machinery",
                Category: r.Machinery?.Category ?? "Equipment",
                OwnerName: "Machinery Owner",
                StartDateUtc: r.StartDate.ToDateTime(TimeOnly.MinValue),
                EndDateUtc: r.EndDate.ToDateTime(TimeOnly.MinValue),
                RentalDays: r.RentalDays,
                DriverSelected: r.DriverRequired,
                TotalAmountPaid: r.TotalPayableAmount,
                Status: r.RentalStatus.ToString()
            ))
            .ToList();

        return new CustomerAnalyticsOverviewResponse(
            DateRangeLabel: dateLabel,
            FromDateUtc: fromDateUtc,
            ToDateUtc: toDateUtc,
            TotalAuctionsParticipated: totalAuctionsParticipated,
            TotalBidsPlaced: totalBidsPlaced,
            LiveBidsCount: liveBidsCount,
            WinningBidsCount: winningAuctionsCount,
            WinningRatePercentage: winningRatePercentage,
            TotalQuantityPurchasedKg: totalQuantityPurchasedKg,
            TotalQuantityPurchasedMan: totalQuantityPurchasedMan,
            TotalCropOrders: totalCropOrders,
            CompletedOrders: completedOrdersCount,
            ActiveOrders: activeOrdersCount,
            CancelledOrders: cancelledOrdersCount,
            PendingOrders: pendingOrdersCount,
            TotalCropSpending: totalCropSpending,
            AverageOrderValue: averageOrderValue,
            HighestOrderValue: highestOrderValue,
            TotalMachineryRentals: totalMachineryRentals,
            UpcomingRentalsCount: upcomingRentalsCount,
            ActiveRentalsCount: activeRentalsCount,
            CompletedRentalsCount: completedRentalsCount,
            CancelledRentalsCount: cancelledRentalsCount,
            TotalMachineryRentalSpending: totalMachineryRentalSpending,
            AverageRentalDurationDays: avgRentalDurationDays,
            RentalsWithDriverCount: rentalsWithDriverCount,
            RentalsWithoutDriverCount: rentalsWithoutDriverCount,
            DriverSpending: driverSpending,
            MachineryOwnedCount: machineryOwnedCount,
            MachineryRentalIncome: machineryRentalIncome,
            TotalReviewsWritten: totalReviewsWritten,
            CropReviewsWrittenCount: cropReviewsCount,
            MachineryReviewsWrittenCount: machineryReviewsCount,
            AverageRatingGiven: avgRatingGiven,
            GivenRatingDistribution: givenRatingDist,
            WishlistCount: wishlistCount,
            CropWishlistCount: cropWishlistCount,
            AuctionWishlistCount: auctionWishlistCount,
            SpendingOverTime: spendingChart,
            BiddingActivityOverTime: biddingChart,
            TopPurchasedCrops: topPurchasedCrops,
            MachineryRentalHistory: rentalHistory
        );
    }

    private static CustomerAnalyticsOverviewResponse CreateEmptyResponse(string customerUserId, string dateLabel, DateTime fromDateUtc, DateTime toDateUtc)
    {
        var emptyDist = new RatingDistributionDto(0, 0, 0, 0, 0);
        var emptyChart = new TimeSeriesChartDto("N/A", "Daily", Array.Empty<TimeSeriesPointDto>());

        return new CustomerAnalyticsOverviewResponse(
            DateRangeLabel: dateLabel,
            FromDateUtc: fromDateUtc,
            ToDateUtc: toDateUtc,
            TotalAuctionsParticipated: 0,
            TotalBidsPlaced: 0,
            LiveBidsCount: 0,
            WinningBidsCount: 0,
            WinningRatePercentage: 0.0,
            TotalQuantityPurchasedKg: 0m,
            TotalQuantityPurchasedMan: 0m,
            TotalCropOrders: 0,
            CompletedOrders: 0,
            ActiveOrders: 0,
            CancelledOrders: 0,
            PendingOrders: 0,
            TotalCropSpending: 0m,
            AverageOrderValue: 0m,
            HighestOrderValue: 0m,
            TotalMachineryRentals: 0,
            UpcomingRentalsCount: 0,
            ActiveRentalsCount: 0,
            CompletedRentalsCount: 0,
            CancelledRentalsCount: 0,
            TotalMachineryRentalSpending: 0m,
            AverageRentalDurationDays: 0.0,
            RentalsWithDriverCount: 0,
            RentalsWithoutDriverCount: 0,
            DriverSpending: 0m,
            MachineryOwnedCount: 0,
            MachineryRentalIncome: 0m,
            TotalReviewsWritten: 0,
            CropReviewsWrittenCount: 0,
            MachineryReviewsWrittenCount: 0,
            AverageRatingGiven: 0.0,
            GivenRatingDistribution: emptyDist,
            WishlistCount: 0,
            CropWishlistCount: 0,
            AuctionWishlistCount: 0,
            SpendingOverTime: emptyChart,
            BiddingActivityOverTime: emptyChart,
            TopPurchasedCrops: Array.Empty<CustomerTopPurchasedCropResponse>(),
            MachineryRentalHistory: Array.Empty<CustomerMachineryRentalHistoryItemResponse>()
        );
    }

    private static List<TimeSeriesPointDto> GenerateDailyPoints(
        DateTime fromDateUtc,
        DateTime toDateUtc,
        Dictionary<DateTime, decimal> dataMap)
    {
        var points = new List<TimeSeriesPointDto>();
        var start = fromDateUtc.Date;
        var end = toDateUtc.Date;

        var totalDays = (end - start).Days + 1;
        int step = 1;
        if (totalDays > 31)
        {
            step = (int)Math.Ceiling(totalDays / 30.0);
        }

        for (var date = start; date <= end; date = date.AddDays(step))
        {
            decimal sum = 0m;
            for (int i = 0; i < step && date.AddDays(i) <= end; i++)
            {
                var curDate = date.AddDays(i);
                if (dataMap.TryGetValue(curDate, out var v))
                {
                    sum += v;
                }
            }
            points.Add(new TimeSeriesPointDto(date.ToString("MMM dd"), date, Math.Round(sum, 2)));
        }

        return points;
    }
}
