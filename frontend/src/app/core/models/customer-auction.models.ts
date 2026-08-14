export interface CustomerAuction {
  id: string;
  cropId: string;
  cropName: string;
  cropType: string;
  variety?: string | null;
  quantity: number;
  unit: string;
  quantityKg: number;
  quantityMan: number;
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

export interface AuctionBid {
  id: string;
  auctionId: string;
  customerProfileId: string;
  customerName: string;
  amount: number;
  bidTimeUtc: string;
  bidStatus: string;
}

export interface AuctionResult {
  auctionId: string;
  cropId: string;
  cropName: string;
  cropType: string;
  quantity: number;
  unit: string;
  quantityMan: number;
  auctionStatus: string;
  hasWinner: boolean;
  winningBidAmount?: number | null;
  winnerCustomerName?: string | null;
  winnerCustomerProfileId?: string | null;
  totalBids: number;
  startTimeUtc: string;
  endTimeUtc: string;
  finalizedAtUtc?: string | null;
  customerResultStatus?: 'WON' | 'LOST' | 'DID NOT BID' | 'NO WINNER' | string | null;
  serverTimeUtc: string;
}

export interface CustomerMyBid {
  bidId: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  primaryImageUrl?: string | null;
  cropType: string;
  quantity: number;
  unit: string;
  quantityMan: number;
  customerBidAmount: number;
  currentHighestBid: number;
  minimumBidIncrement: number;
  auctionStatus: string;
  customerBidStatus: 'HIGHEST BID' | 'OUTBID' | 'WON' | 'LOST' | string;
  bidTimeUtc: string;
  startTimeUtc: string;
  endTimeUtc: string;
  serverTimeUtc: string;
}

export interface AuctionPayment {
  paymentId: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  cropType: string;
  quantity: number;
  unit: string;
  quantityMan: number;
  winningBidAmount: number;
  totalPayableAmount: number;
  currency: string;
  paymentMethod: string;
  paymentStatus: 'PENDING' | 'PROCESSING' | 'PAID' | 'FAILED' | string;
  transactionReference: string;
  winnerCustomerName: string;
  farmerName: string;
  createdAtUtc: string;
  paidAtUtc?: string | null;
  serverTimeUtc: string;
}

export interface CustomerPaymentHistory {
  paymentId: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  primaryImageUrl?: string | null;
  cropType: string;
  quantity: number;
  unit: string;
  quantityMan: number;
  winningBidAmount: number;
  totalPayableAmount: number;
  currency: string;
  paymentMethod: string;
  paymentStatus: string;
  transactionReference: string;
  createdAtUtc: string;
  paidAtUtc?: string | null;
}
