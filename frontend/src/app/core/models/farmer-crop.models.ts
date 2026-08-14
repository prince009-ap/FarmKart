export type CropStatus = 'Planned' | 'Growing' | 'ReadyForHarvest' | 'Harvested' | 'Sold' | 'Archived';
export type AreaUnit = 'Bigha' | 'Acre' | 'Hectare';

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
