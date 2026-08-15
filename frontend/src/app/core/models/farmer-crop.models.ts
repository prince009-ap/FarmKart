export type CropStatus = 'Planned' | 'Growing' | 'ReadyForHarvest' | 'Harvested' | 'Sold' | 'Archived';
export type AreaUnit = 'Bigha' | 'Acre' | 'Hectare';
export type CropStockUnit = 'Kilogram' | 'Quintal' | 'Ton' | 'Kg';

export interface CropImage {
  id: string;
  cropId: string;
  imageUrl: string;
  isPrimary: boolean;
  displayOrder: number;
  createdAtUtc: string;
}

export interface FarmerCrop {
  id: string;
  farmerProfileId: string;
  farmerName: string;
  cropName: string;
  cropType: string;
  variety?: string | null;
  area: number;
  areaUnit: AreaUnit | string;
  sowingDate?: string | null;
  expectedHarvestDate?: string | null;
  actualHarvestDate?: string | null;
  quantity: number;
  unit: string;
  qualityGrade?: string | null;
  description?: string | null;
  status: CropStatus | string;
  primaryImageUrl?: string | null;
  images?: CropImage[];
  availableQuantityFormatted?: string;
  availableQuantityKg?: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateCropRequest {
  cropName: string;
  cropType: string;
  variety?: string | null;
  area: number;
  areaUnit: AreaUnit | string;
  sowingDate?: string | null;
  expectedHarvestDate?: string | null;
  actualHarvestDate?: string | null;
  status?: CropStatus | string;
  description?: string | null;
}

export interface UpdateCropRequest {
  cropName: string;
  cropType: string;
  variety?: string | null;
  area: number;
  areaUnit: AreaUnit | string;
  sowingDate?: string | null;
  expectedHarvestDate?: string | null;
  actualHarvestDate?: string | null;
  status?: CropStatus | string;
  description?: string | null;
}

export interface CropStockSummary {
  cropId: string;
  cropName: string;
  cropStatus: string;
  availableQuantityKg: number;
  availableQuantityFormatted: string;
  displayUnit: string;
  lastUpdatedUtc?: string | null;
  totalTransactionsCount: number;
}

export interface CropStockTransaction {
  id: string;
  cropId: string;
  quantity: number;
  unit: string;
  quantityInBaseUnit: number;
  transactionType: string;
  notes?: string | null;
  createdAtUtc: string;
}

export interface AddCropStockRequest {
  quantity: number;
  unit: string;
  transactionType?: string;
  notes?: string | null;
}

export interface FarmerAuctionPaymentSummary {
  totalWinningAmount: number;
  paidAmount: number;
  pendingAmount: number;
  totalPaidCount: number;
  totalPendingCount: number;
}

export interface FarmerAuction {
  id: string;
  cropId: string;
  cropName: string;
  variety?: string | null;
  primaryImageUrl?: string | null;
  quantity: number;
  unit: string;
  quantityKg: number;
  quantityMan: number;
  availableStockKg: number;
  reservedStockKg: number;
  remainingUnreservedStockKg: number;
  startingBidPrice: number;
  minimumBidIncrement: number;
  startTimeUtc: string;
  endTimeUtc: string;
  status: string;
  description?: string | null;
  totalBids: number;
  currentHighestBid: number;
  totalRequestedQuantityKg: number;
  totalRequestedQuantityMan: number;
  demandPercentage: number;
  totalAllocatedQuantityKg?: number;
  totalAllocatedQuantityMan?: number;
  remainingQuantityKg?: number;
  winnersCount?: number;
  winningBidAmount?: number | null;
  paymentSummary?: FarmerAuctionPaymentSummary | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  serverTimeUtc: string;
}

export interface FarmerAuctionBid {
  bidId: string;
  auctionId: string;
  customerProfileId: string;
  customerName: string;
  requestedQuantityKg: number;
  requestedQuantityMan: number;
  bidAmountPerMan: number;
  bidTimeUtc: string;
  bidStatus: string;
  allocationStatus?: string | null;
}

export interface FarmerAuctionSummaryCounts {
  totalAuctions: number;
  upcomingCount: number;
  liveCount: number;
  endedCount: number;
  cancelledCount: number;
}

export interface CreateFarmerAuctionRequest {
  cropId: string;
  quantity: number;
  unit: string;
  startingBidPrice: number;
  minimumBidIncrement: number;
  startTimeUtc: string;
  duration: string;
  description?: string | null;
}
