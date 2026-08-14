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

export interface FarmerAuction {
  id: string; cropId: string; cropName: string; quantity: number; unit: string; quantityKg: number; quantityMan: number;
  availableStockKg: number; reservedStockKg: number; remainingUnreservedStockKg: number;
  startingBidPrice: number; minimumBidIncrement: number; startTimeUtc: string; endTimeUtc: string;
  status: string; description?: string | null; createdAtUtc: string; updatedAtUtc: string;
  serverTimeUtc: string;
}

export interface CreateFarmerAuctionRequest {
  cropId: string; quantity: number; unit: string; startingBidPrice: number; minimumBidIncrement: number;
  startTimeUtc: string; duration: string; description?: string | null;
}
