using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record CustomerAnalyticsOverviewResponse(
    // Date Range Info
    string DateRangeLabel,
    DateTime FromDateUtc,
    DateTime ToDateUtc,

    // Overview Cards
    int TotalAuctionsParticipated,
    int TotalBidsPlaced,
    int LiveBidsCount,
    int WinningBidsCount,
    double WinningRatePercentage,

    decimal TotalQuantityPurchasedKg,
    decimal TotalQuantityPurchasedMan,

    int TotalCropOrders,
    int CompletedOrders,
    int ActiveOrders,
    int CancelledOrders,
    int PendingOrders,

    decimal TotalCropSpending,
    decimal AverageOrderValue,
    decimal HighestOrderValue,

    // Machinery Metrics (Rented by Customer)
    int TotalMachineryRentals,
    int UpcomingRentalsCount,
    int ActiveRentalsCount,
    int CompletedRentalsCount,
    int CancelledRentalsCount,
    decimal TotalMachineryRentalSpending,
    double AverageRentalDurationDays,

    // Driver Analytics
    int RentalsWithDriverCount,
    int RentalsWithoutDriverCount,
    decimal DriverSpending,

    // Customer Who Owns Machinery (Bi-directional)
    int MachineryOwnedCount,
    decimal MachineryRentalIncome,

    // Review Analytics
    int TotalReviewsWritten,
    int CropReviewsWrittenCount,
    int MachineryReviewsWrittenCount,
    double AverageRatingGiven,
    RatingDistributionDto GivenRatingDistribution,

    // Wishlist Analytics
    int WishlistCount,
    int CropWishlistCount,
    int AuctionWishlistCount,

    // Time Series Charts
    TimeSeriesChartDto SpendingOverTime,
    TimeSeriesChartDto BiddingActivityOverTime,

    // Top Rankings & Tables
    IReadOnlyList<CustomerTopPurchasedCropResponse> TopPurchasedCrops,
    IReadOnlyList<CustomerMachineryRentalHistoryItemResponse> MachineryRentalHistory
);

public record CustomerTopPurchasedCropResponse(
    Guid CropId,
    string CropName,
    string CropType,
    decimal TotalPurchasedKg,
    decimal TotalPurchasedMan,
    decimal TotalSpending,
    int OrdersCount
);

public record CustomerMachineryRentalHistoryItemResponse(
    Guid RentalId,
    Guid MachineryId,
    string MachineryName,
    string Category,
    string OwnerName,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    int RentalDays,
    bool DriverSelected,
    decimal TotalAmountPaid,
    string Status
);
