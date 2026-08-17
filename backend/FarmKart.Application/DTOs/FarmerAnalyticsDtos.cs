using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record FarmerAnalyticsOverviewResponse(
    // Date Range Info
    string DateRangeLabel,
    DateTime FromDateUtc,
    DateTime ToDateUtc,

    // Overview Cards
    int TotalAuctions,
    int LiveAuctions,
    int UpcomingAuctions,
    int CompletedAuctions,

    decimal TotalQuantityListedKg,
    decimal TotalQuantityListedMan,
    decimal TotalQuantitySoldKg,
    decimal TotalQuantitySoldMan,
    decimal TotalQuantityRemainingKg,

    int TotalOrders,
    int CompletedOrders,
    int ActiveOrders,
    int CancelledOrders,
    int PendingOrders,

    decimal TotalRevenue,

    double AverageFarmerRating,
    int TotalFarmerReviews,
    RatingDistributionDto FarmerRatingDistribution,

    // Machinery Metrics (Owned by Farmer)
    int MachineryListedCount,
    int ActiveMachineryRentalsCount,
    int CompletedMachineryRentalsCount,
    decimal MachineryRentalIncome,
    double AverageMachineryRating,
    int TotalMachineryReviews,

    // Driver Analytics
    int RentalsWithDriverCount,
    int RentalsWithoutDriverCount,
    decimal DriverRevenue,

    // Farmer Who Rents Machinery (Bi-directional)
    int MachineryRentedCount,
    decimal MachineryRentalSpending,

    // Performance & Auction Breakdown
    int TotalBidsReceived,
    int AverageBidsPerAuction,
    decimal HighestBidAmount,
    decimal AverageWinningBidAmount,

    // Time Series Charts
    TimeSeriesChartDto RevenueOverTime,
    TimeSeriesChartDto QuantitySoldOverTime,
    TimeSeriesChartDto OrdersOverTime,

    // Top Rankings & Tables
    IReadOnlyList<FarmerTopCropResponse> TopSellingCrops,
    IReadOnlyList<FarmerAuctionPerformanceItemResponse> AuctionPerformanceTable,
    IReadOnlyList<FarmerTopMachineryResponse> TopRentedMachinery
);

public record FarmerAuctionPerformanceItemResponse(
    Guid AuctionId,
    string CropName,
    decimal TotalQuantityKg,
    decimal StartingPrice,
    decimal HighestBid,
    decimal WinningPricePerMan,
    int TotalBids,
    string Status,
    DateTime CreatedAtUtc
);

public record FarmerTopCropResponse(
    Guid CropId,
    string CropName,
    string CropType,
    decimal TotalQuantitySoldKg,
    decimal TotalQuantitySoldMan,
    decimal TotalRevenue,
    int TotalOrdersCount
);

public record FarmerTopMachineryResponse(
    Guid MachineryId,
    string Name,
    string Category,
    int TotalRentals,
    decimal TotalIncome,
    double AverageRating
);
