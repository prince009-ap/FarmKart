// Farmer-domain TypeScript models for FarmKart
// FarmSizeUnit mirrors FarmKart.Domain.Enums.FarmSizeUnit
export type FarmSizeUnit = 'Vigha' | 'Acre' | 'Hectare';

export interface FarmerProfile {
  userId: string;
  fullName: string;
  email: string;
  phone: string;
  address: string;
  farmName: string | null;
  farmSize: number | null;
  farmSizeUnit: FarmSizeUnit | null;
  farmLocation: string | null;
}

export interface FarmerProfileUpdateRequest {
  fullName: string;
  phone: string;
  address: string;
  farmName: string | null;
  farmSize: number | null;
  farmSizeUnit: FarmSizeUnit | null;
  farmLocation: string | null;
}
