export interface CustomerAuction {
  id: string;
  cropId: string;
  cropName: string;
  cropType: string;
  variety?: string | null;
  quantity: number;
  unit: string;
  quantityKg: number;
  startingBidPrice: number;
  currentHighestBid: number;
  minimumBidIncrement: number;
  farmerName: string;
  farmLocation: string;
  startTimeUtc: string;
  endTimeUtc: string;
  status: 'LIVE' | 'UPCOMING' | 'ENDED' | string;
  primaryImageUrl?: string | null;
  images: string[];
  description?: string | null;
  createdAtUtc: string;
  serverTimeUtc: string;
}

export interface CustomerAuctionFilter {
  search?: string;
  category?: string;
  status?: string;
  location?: string;
  sortBy?: string;
}
