export interface FarmerPublicReviewResponse {
  reviewId: string;
  reviewerName: string;
  rating: number;
  comment?: string;
  createdAtUtc: string;
}

export interface FarmerPublicAuctionResponse {
  auctionId: string;
  title: string;
  cropName: string;
  cropType: string;
  startingPrice: number;
  totalQuantity: number;
  unit: string;
  status: string;
  startDateUtc: string;
  endDateUtc: string;
  primaryImageUrl?: string;
}

export interface FarmerPublicMachineryResponse {
  machineryId: string;
  name: string;
  category: string;
  brand?: string;
  model?: string;
  dailyRent: number;
  driverAvailable: boolean;
  availabilityStatus: string;
  averageRating: number;
  reviewCount: number;
  primaryImageUrl?: string;
  location?: string;
  city?: string;
  state?: string;
}

export interface FarmerPublicProfileResponse {
  farmerId: string;
  userId: string;
  fullName: string;
  farmName?: string;
  location?: string;
  city?: string;
  state?: string;
  memberSinceUtc: string;
  averageRating: number;
  totalReviews: number;
  reviews: FarmerPublicReviewResponse[];
  activeAuctions: FarmerPublicAuctionResponse[];
  machinery: FarmerPublicMachineryResponse[];
}
