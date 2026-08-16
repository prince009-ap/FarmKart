export type WishlistItemType = 'Crop' | 'Auction' | 'Machinery';

export interface AddWishlistItemRequest {
  itemType: WishlistItemType;
  itemId: string;
}

export interface WishlistItemResponse {
  id: string;
  itemType: WishlistItemType;
  itemId: string;
  createdAtUtc: string;

  // Enriched Crop fields
  cropName?: string;
  cropType?: string;
  variety?: string;
  farmerName?: string;
  primaryImageUrl?: string | null;
  cropStatus?: string;

  // Enriched Auction fields
  auctionStatus?: string;
  startingBidPrice?: number;
  currentHighestBid?: number;
  quantityKg?: number;
  quantityMan?: number;
  auctionStartTimeUtc?: string;
  auctionEndTimeUtc?: string;
  serverTimeUtc?: string;
  isAuctionExpired?: boolean;
  isItemAvailable?: boolean;
}

export interface WishlistCountResponse {
  total: number;
  cropCount: number;
  auctionCount: number;
}

export interface WishlistStatusResponse {
  isFavorited: boolean;
  wishlistItemId?: string;
}
