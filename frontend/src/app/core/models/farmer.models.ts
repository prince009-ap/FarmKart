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

export type JobStatus = 'Draft' | 'Open' | 'InProgress' | 'Completed' | 'Cancelled';

export interface FarmerJob {
  id: string;
  title: string;
  description: string;
  workCategory: string;
  cropType: string | null;
  workersRequired: number;
  requiredExperience: number;
  wagePerDay: number;
  startDate: string;
  endDate: string;
  workingHours: string;
  farmLocation: string;
  farmSize: number | null;
  foodProvided: boolean;
  accommodationProvided: boolean;
  isUrgent: boolean;
  status: JobStatus;
  createdAtUtc: string;
}

export interface FarmerJobRequest {
  title: string;
  description: string;
  workCategory: string;
  cropType: string | null;
  workersRequired: number;
  requiredExperience: number;
  wagePerDay: number;
  startDate: string;
  endDate: string;
  workingHours: string;
  farmLocation: string;
  farmSize: number | null;
  foodProvided: boolean;
  accommodationProvided: boolean;
  isUrgent: boolean;
}
