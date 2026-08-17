export enum AnalyticsDateRange {
  Today = 'Today',
  Last7Days = 'Last7Days',
  Last30Days = 'Last30Days',
  ThisMonth = 'ThisMonth',
  LastMonth = 'LastMonth',
  ThisYear = 'ThisYear',
  Custom = 'Custom'
}

export interface AnalyticsDateRangeRequest {
  range: AnalyticsDateRange;
  customStartDateUtc?: string | null;
  customEndDateUtc?: string | null;
}

export interface TimeSeriesPoint {
  label: string;
  dateUtc: string;
  value: number;
}

export interface TimeSeriesChart {
  metricName: string;
  timeGroup: string;
  points: TimeSeriesPoint[];
}

export interface RatingDistribution {
  fiveStar: number;
  fourStar: number;
  threeStar: number;
  twoStar: number;
  oneStar: number;
}

export interface FarmerTopCrop {
  cropId: string;
  cropName: string;
  cropType: string;
  totalQuantitySoldKg: number;
  totalQuantitySoldMan: number;
  totalRevenue: number;
  totalOrdersCount: number;
}

export interface FarmerAuctionPerformanceItem {
  auctionId: string;
  cropName: string;
  totalQuantityKg: number;
  startingPrice: number;
  highestBid: number;
  winningPricePerMan: number;
  totalBids: number;
  status: string;
  createdAtUtc: string;
}

export interface FarmerTopMachinery {
  machineryId: string;
  name: string;
  category: string;
  totalRentals: number;
  totalIncome: number;
  averageRating: number;
}

export interface FarmerAnalyticsOverview {
  dateRangeLabel: string;
  fromDateUtc: string;
  toDateUtc: string;

  totalAuctions: number;
  liveAuctions: number;
  upcomingAuctions: number;
  completedAuctions: number;

  totalQuantityListedKg: number;
  totalQuantityListedMan: number;
  totalQuantitySoldKg: number;
  totalQuantitySoldMan: number;
  totalQuantityRemainingKg: number;

  totalOrders: number;
  completedOrders: number;
  activeOrders: number;
  cancelledOrders: number;
  pendingOrders: number;

  totalRevenue: number;

  averageFarmerRating: number;
  totalFarmerReviews: number;
  farmerRatingDistribution: RatingDistribution;

  machineryListedCount: number;
  activeMachineryRentalsCount: number;
  completedMachineryRentalsCount: number;
  machineryRentalIncome: number;
  averageMachineryRating: number;
  totalMachineryReviews: number;

  rentalsWithDriverCount: number;
  rentalsWithoutDriverCount: number;
  driverRevenue: number;

  machineryRentedCount: number;
  machineryRentalSpending: number;

  totalBidsReceived: number;
  averageBidsPerAuction: number;
  highestBidAmount: number;
  averageWinningBidAmount: number;

  revenueOverTime: TimeSeriesChart;
  quantitySoldOverTime: TimeSeriesChart;
  ordersOverTime: TimeSeriesChart;

  topSellingCrops: FarmerTopCrop[];
  auctionPerformanceTable: FarmerAuctionPerformanceItem[];
  topRentedMachinery: FarmerTopMachinery[];
}

export interface CustomerTopPurchasedCrop {
  cropId: string;
  cropName: string;
  cropType: string;
  totalPurchasedKg: number;
  totalPurchasedMan: number;
  totalSpending: number;
  ordersCount: number;
}

export interface CustomerMachineryRentalHistoryItem {
  rentalId: string;
  machineryId: string;
  machineryName: string;
  category: string;
  ownerName: string;
  startDateUtc: string;
  endDateUtc: string;
  rentalDays: number;
  driverSelected: boolean;
  totalAmountPaid: number;
  status: string;
}

export interface CustomerAnalyticsOverview {
  dateRangeLabel: string;
  fromDateUtc: string;
  toDateUtc: string;

  totalAuctionsParticipated: number;
  totalBidsPlaced: number;
  liveBidsCount: number;
  winningBidsCount: number;
  winningRatePercentage: number;

  totalQuantityPurchasedKg: number;
  totalQuantityPurchasedMan: number;

  totalCropOrders: number;
  completedOrders: number;
  activeOrders: number;
  cancelledOrders: number;
  pendingOrders: number;

  totalCropSpending: number;
  averageOrderValue: number;
  highestOrderValue: number;

  totalMachineryRentals: number;
  upcomingRentalsCount: number;
  activeRentalsCount: number;
  completedRentalsCount: number;
  cancelledRentalsCount: number;
  totalMachineryRentalSpending: number;
  averageRentalDurationDays: number;

  rentalsWithDriverCount: number;
  rentalsWithoutDriverCount: number;
  driverSpending: number;

  machineryOwnedCount: number;
  machineryRentalIncome: number;

  totalReviewsWritten: number;
  cropReviewsWrittenCount: number;
  machineryReviewsWrittenCount: number;
  averageRatingGiven: number;
  givenRatingDistribution: RatingDistribution;

  wishlistCount: number;
  cropWishlistCount: number;
  auctionWishlistCount: number;

  spendingOverTime: TimeSeriesChart;
  biddingActivityOverTime: TimeSeriesChart;

  topPurchasedCrops: CustomerTopPurchasedCrop[];
  machineryRentalHistory: CustomerMachineryRentalHistoryItem[];
}
