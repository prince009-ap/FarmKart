using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Helpers;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class FarmerAnalyticsService(FarmKartDbContext db) : IFarmerAnalyticsService
{
    private readonly FarmKartDbContext _db = db;

    public async Task<FarmerAnalyticsOverviewResponse> GetFarmerAnalyticsAsync(
        string farmerUserId,
        AnalyticsDateRangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(farmerUserId, out var farmerGuid))
        {
            throw new ArgumentException("Invalid farmer user ID format", nameof(farmerUserId));
        }

        var farmer = await _db.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == farmerGuid, cancellationToken);

        var (fromDateUtc, toDateUtc, dateLabel) = AnalyticsDateHelper.CalculateDateRange(request);

        if (farmer == null)
        {
            return CreateEmptyResponse(farmerUserId, dateLabel, fromDateUtc, toDateUtc);
        }

        var farmerProfileId = farmer.Id;

        // 1. Auctions
        var auctionsQuery = _db.Auctions
            .AsNoTracking()
            .Include(a => a.CropListing)
                .ThenInclude(cl => cl.Crop)
            .Include(a => a.Bids)
            .Include(a => a.Allocations)
            .Where(a => a.FarmerProfileId == farmerProfileId && a.CreatedAtUtc >= fromDateUtc && a.CreatedAtUtc <= toDateUtc);

        var auctions = await auctionsQuery.ToListAsync(cancellationToken);

        int totalAuctions = auctions.Count;
        int liveAuctions = auctions.Count(a => a.AuctionStatus == AuctionStatus.Live);
        int upcomingAuctions = auctions.Count(a => a.AuctionStatus == AuctionStatus.Scheduled);
        int completedAuctions = auctions.Count(a => a.AuctionStatus is AuctionStatus.Ended or AuctionStatus.Finalized);

        decimal totalQuantityListedKg = auctions.Sum(a => a.CropListing?.QuantityForSale ?? 0m);
        decimal totalQuantityListedMan = totalQuantityListedKg / 20m;

        // 2. Allocations & Quantity Sold
        var allocationsQuery = _db.AuctionAllocations
            .AsNoTracking()
            .Include(al => al.Auction)
            .Where(al => al.Auction.FarmerProfileId == farmerProfileId && al.FinalizedAtUtc >= fromDateUtc && al.FinalizedAtUtc <= toDateUtc);

        var allocations = await allocationsQuery.ToListAsync(cancellationToken);

        decimal totalQuantitySoldKg = allocations
            .Where(al => al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon)
            .Sum(al => al.AllocatedQuantityKg);
        decimal totalQuantitySoldMan = totalQuantitySoldKg / 20m;

        decimal totalQuantityRemainingKg = auctions.Sum(a =>
        {
            decimal listed = a.CropListing?.QuantityForSale ?? 0m;
            decimal sold = a.Allocations
                .Where(al => al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon)
                .Sum(al => al.AllocatedQuantityKg);
            return Math.Max(0m, listed - sold);
        });

        // 3. Crop Orders & Revenue
        var ordersQuery = _db.AuctionOrders
            .AsNoTracking()
            .Include(o => o.Crop)
            .Where(o => o.FarmerProfileId == farmerProfileId && o.CreatedAtUtc >= fromDateUtc && o.CreatedAtUtc <= toDateUtc);

        var orders = await ordersQuery.ToListAsync(cancellationToken);

        int totalOrders = orders.Count;
        int completedOrdersCount = orders.Count(o => o.Status is OrderStatus.Completed or OrderStatus.Delivered);
        int activeOrdersCount = orders.Count(o => o.Status is OrderStatus.Confirmed or OrderStatus.ReadyForPickup or OrderStatus.Dispatched or OrderStatus.PickedUp);
        int cancelledOrdersCount = orders.Count(o => o.Status == OrderStatus.Cancelled);
        int pendingOrdersCount = orders.Count(o => o.Status == OrderStatus.Pending);

        decimal totalRevenue = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .Sum(o => o.TotalAmount);

        // 4. Farmer Ratings & Reviews (Order reviews ONLY)
        var farmerReviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.RevieweeUserId == farmerUserId && r.RelatedEntityType == ReviewEntityType.Order && r.CreatedAtUtc >= fromDateUtc && r.CreatedAtUtc <= toDateUtc)
            .ToListAsync(cancellationToken);

        int totalFarmerReviews = farmerReviews.Count;
        double avgFarmerRating = totalFarmerReviews > 0 ? Math.Round(farmerReviews.Average(r => r.Rating), 1) : 0.0;
        var farmerRatingDist = new RatingDistributionDto(
            FiveStar: farmerReviews.Count(r => r.Rating == 5),
            FourStar: farmerReviews.Count(r => r.Rating == 4),
            ThreeStar: farmerReviews.Count(r => r.Rating == 3),
            TwoStar: farmerReviews.Count(r => r.Rating == 2),
            OneStar: farmerReviews.Count(r => r.Rating == 1)
        );

        // 5. Machinery Metrics (Farmer as OWNER)
        var ownedMachinery = await _db.Machinery
            .AsNoTracking()
            .Include(m => m.Images)
            .Where(m => m.OwnerUserId == farmerUserId && m.IsActive)
            .ToListAsync(cancellationToken);

        int machineryListedCount = ownedMachinery.Count;

        var ownedRentals = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery)
            .Where(r => r.OwnerUserId == farmerUserId && r.CreatedAtUtc >= fromDateUtc && r.CreatedAtUtc <= toDateUtc)
            .ToListAsync(cancellationToken);

        int activeMachineryRentalsCount = ownedRentals.Count(r => r.RentalStatus is RentalStatus.Booked or RentalStatus.Confirmed or RentalStatus.ReadyForHandover or RentalStatus.RentedOut);
        int completedMachineryRentalsCount = ownedRentals.Count(r => r.RentalStatus is RentalStatus.Returned or RentalStatus.Completed);

        decimal machineryRentalIncome = ownedRentals
            .Where(r => r.RentalStatus != RentalStatus.Cancelled)
            .Sum(r => r.TotalPayableAmount);

        // Machinery Reviews for owned machinery
        var ownedRentalIds = ownedRentals.Select(r => r.Id).ToList();
        var machineryReviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.RelatedEntityType == ReviewEntityType.MachineryRental && r.RelatedEntityId.HasValue && ownedRentalIds.Contains(r.RelatedEntityId.Value))
            .ToListAsync(cancellationToken);

        int totalMachineryReviews = machineryReviews.Count;
        double avgMachineryRating = totalMachineryReviews > 0 ? Math.Round(machineryReviews.Average(r => r.Rating), 1) : 0.0;

        int rentalsWithDriverCount = ownedRentals.Count(r => r.DriverRequired);
        int rentalsWithoutDriverCount = ownedRentals.Count(r => !r.DriverRequired);
        decimal driverRevenue = ownedRentals
            .Where(r => r.RentalStatus != RentalStatus.Cancelled && r.DriverRequired)
            .Sum(r => r.DriverAmount);

        // 6. Bi-directional Machinery Metrics (Farmer as RENTER)
        var farmerAsRenterRentals = await _db.MachineryRentals
            .AsNoTracking()
            .Where(r => r.RenterUserId == farmerUserId && r.CreatedAtUtc >= fromDateUtc && r.CreatedAtUtc <= toDateUtc)
            .ToListAsync(cancellationToken);

        int machineryRentedCount = farmerAsRenterRentals.Count;
        decimal machineryRentalSpending = farmerAsRenterRentals
            .Where(r => r.RentalStatus != RentalStatus.Cancelled)
            .Sum(r => r.TotalPayableAmount);

        // 7. Bidding & Auction Performance Breakdown
        var allBids = auctions.SelectMany(a => a.Bids).ToList();
        int totalBidsReceived = allBids.Count;
        int avgBidsPerAuction = totalAuctions > 0 ? (int)Math.Round((double)totalBidsReceived / totalAuctions) : 0;
        decimal highestBidAmount = allBids.Count > 0 ? allBids.Max(b => b.Amount) : 0m;
        decimal avgWinningBidAmount = allocations.Count > 0 ? Math.Round(allocations.Average(al => al.WinningBidAmountPerMan), 2) : 0m;

        // 8. Time-Series Charts (Grouping by Day across full date range)
        var revenueDataMap = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.CreatedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));

        var quantitySoldDataMap = allocations
            .Where(al => al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon)
            .GroupBy(al => al.FinalizedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Sum(al => al.AllocatedQuantityKg));

        var ordersDataMap = orders
            .GroupBy(o => o.CreatedAtUtc.Date)
            .ToDictionary(g => g.Key, g => (decimal)g.Count());

        var revenuePoints = GenerateDailyPoints(fromDateUtc, toDateUtc, revenueDataMap);
        var quantitySoldPoints = GenerateDailyPoints(fromDateUtc, toDateUtc, quantitySoldDataMap);
        var ordersPoints = GenerateDailyPoints(fromDateUtc, toDateUtc, ordersDataMap);

        var revenueChart = new TimeSeriesChartDto("Revenue Over Time", "Daily", revenuePoints);
        var quantitySoldChart = new TimeSeriesChartDto("Quantity Sold Over Time (Kg)", "Daily", quantitySoldPoints);
        var ordersChart = new TimeSeriesChartDto("Orders Over Time", "Daily", ordersPoints);

        // 9. Top Rankings & Tables
        var topSellingCrops = orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.Crop != null)
            .GroupBy(o => o.CropId)
            .Select(g =>
            {
                var crop = g.First().Crop;
                decimal soldKg = g.Sum(o => o.AllocatedQuantityKg);
                decimal rev = g.Sum(o => o.TotalAmount);
                return new FarmerTopCropResponse(
                    CropId: g.Key,
                    CropName: crop?.CropName ?? "Crop",
                    CropType: crop?.CropType ?? "N/A",
                    TotalQuantitySoldKg: soldKg,
                    TotalQuantitySoldMan: Math.Round(soldKg / 20m, 2),
                    TotalRevenue: rev,
                    TotalOrdersCount: g.Count()
                );
            })
            .OrderByDescending(c => c.TotalQuantitySoldKg)
            .Take(5)
            .ToList();

        var auctionTable = auctions
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(10)
            .Select(a =>
            {
                decimal topBid = a.Bids.Count > 0 ? a.Bids.Max(b => b.Amount) : a.CurrentHighestBid;
                decimal winPrice = a.Allocations.Count > 0 ? a.Allocations.Average(al => al.WinningBidAmountPerMan) : 0m;

                return new FarmerAuctionPerformanceItemResponse(
                    AuctionId: a.Id,
                    CropName: a.CropListing?.Crop?.CropName ?? "Crop Listing",
                    TotalQuantityKg: a.CropListing?.QuantityForSale ?? 0m,
                    StartingPrice: a.StartingPrice,
                    HighestBid: topBid,
                    WinningPricePerMan: Math.Round(winPrice, 2),
                    TotalBids: a.Bids.Count,
                    Status: a.AuctionStatus.ToString(),
                    CreatedAtUtc: a.CreatedAtUtc
                );
            })
            .ToList();

        var topMachinery = ownedRentals
            .Where(r => r.Machinery != null)
            .GroupBy(r => r.MachineryId)
            .Select(g =>
            {
                var m = g.First().Machinery;
                var mRevs = machineryReviews.Where(rev => rev.RelatedEntityId.HasValue && g.Select(r => r.Id).Contains(rev.RelatedEntityId.Value)).ToList();
                double mAvg = mRevs.Count > 0 ? Math.Round(mRevs.Average(r => r.Rating), 1) : 0.0;

                return new FarmerTopMachineryResponse(
                    MachineryId: g.Key,
                    Name: m?.Name ?? "Machinery",
                    Category: m?.Category ?? "Equipment",
                    TotalRentals: g.Count(),
                    TotalIncome: g.Where(r => r.RentalStatus != RentalStatus.Cancelled).Sum(r => r.TotalPayableAmount),
                    AverageRating: mAvg
                );
            })
            .OrderByDescending(m => m.TotalRentals)
            .Take(5)
            .ToList();

        return new FarmerAnalyticsOverviewResponse(
            DateRangeLabel: dateLabel,
            FromDateUtc: fromDateUtc,
            ToDateUtc: toDateUtc,
            TotalAuctions: totalAuctions,
            LiveAuctions: liveAuctions,
            UpcomingAuctions: upcomingAuctions,
            CompletedAuctions: completedAuctions,
            TotalQuantityListedKg: totalQuantityListedKg,
            TotalQuantityListedMan: Math.Round(totalQuantityListedMan, 2),
            TotalQuantitySoldKg: totalQuantitySoldKg,
            TotalQuantitySoldMan: Math.Round(totalQuantitySoldMan, 2),
            TotalQuantityRemainingKg: totalQuantityRemainingKg,
            TotalOrders: totalOrders,
            CompletedOrders: completedOrdersCount,
            ActiveOrders: activeOrdersCount,
            CancelledOrders: cancelledOrdersCount,
            PendingOrders: pendingOrdersCount,
            TotalRevenue: totalRevenue,
            AverageFarmerRating: avgFarmerRating,
            TotalFarmerReviews: totalFarmerReviews,
            FarmerRatingDistribution: farmerRatingDist,
            MachineryListedCount: machineryListedCount,
            ActiveMachineryRentalsCount: activeMachineryRentalsCount,
            CompletedMachineryRentalsCount: completedMachineryRentalsCount,
            MachineryRentalIncome: machineryRentalIncome,
            AverageMachineryRating: avgMachineryRating,
            TotalMachineryReviews: totalMachineryReviews,
            RentalsWithDriverCount: rentalsWithDriverCount,
            RentalsWithoutDriverCount: rentalsWithoutDriverCount,
            DriverRevenue: driverRevenue,
            MachineryRentedCount: machineryRentedCount,
            MachineryRentalSpending: machineryRentalSpending,
            TotalBidsReceived: totalBidsReceived,
            AverageBidsPerAuction: avgBidsPerAuction,
            HighestBidAmount: highestBidAmount,
            AverageWinningBidAmount: avgWinningBidAmount,
            RevenueOverTime: revenueChart,
            QuantitySoldOverTime: quantitySoldChart,
            OrdersOverTime: ordersChart,
            TopSellingCrops: topSellingCrops,
            AuctionPerformanceTable: auctionTable,
            TopRentedMachinery: topMachinery
        );
    }

    private static FarmerAnalyticsOverviewResponse CreateEmptyResponse(string farmerUserId, string dateLabel, DateTime fromDateUtc, DateTime toDateUtc)
    {
        var emptyDist = new RatingDistributionDto(0, 0, 0, 0, 0);
        var emptyChart = new TimeSeriesChartDto("N/A", "Daily", Array.Empty<TimeSeriesPointDto>());

        return new FarmerAnalyticsOverviewResponse(
            DateRangeLabel: dateLabel,
            FromDateUtc: fromDateUtc,
            ToDateUtc: toDateUtc,
            TotalAuctions: 0,
            LiveAuctions: 0,
            UpcomingAuctions: 0,
            CompletedAuctions: 0,
            TotalQuantityListedKg: 0m,
            TotalQuantityListedMan: 0m,
            TotalQuantitySoldKg: 0m,
            TotalQuantitySoldMan: 0m,
            TotalQuantityRemainingKg: 0m,
            TotalOrders: 0,
            CompletedOrders: 0,
            ActiveOrders: 0,
            CancelledOrders: 0,
            PendingOrders: 0,
            TotalRevenue: 0m,
            AverageFarmerRating: 0.0,
            TotalFarmerReviews: 0,
            FarmerRatingDistribution: emptyDist,
            MachineryListedCount: 0,
            ActiveMachineryRentalsCount: 0,
            CompletedMachineryRentalsCount: 0,
            MachineryRentalIncome: 0m,
            AverageMachineryRating: 0.0,
            TotalMachineryReviews: 0,
            RentalsWithDriverCount: 0,
            RentalsWithoutDriverCount: 0,
            DriverRevenue: 0m,
            MachineryRentedCount: 0,
            MachineryRentalSpending: 0m,
            TotalBidsReceived: 0,
            AverageBidsPerAuction: 0,
            HighestBidAmount: 0m,
            AverageWinningBidAmount: 0m,
            RevenueOverTime: emptyChart,
            QuantitySoldOverTime: emptyChart,
            OrdersOverTime: emptyChart,
            TopSellingCrops: Array.Empty<FarmerTopCropResponse>(),
            AuctionPerformanceTable: Array.Empty<FarmerAuctionPerformanceItemResponse>(),
            TopRentedMachinery: Array.Empty<FarmerTopMachineryResponse>()
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
