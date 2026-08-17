export type FarmSizeUnit = 'Vigha' | 'Acre' | 'Hectare';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterFarmerRequest {
  fullName: string;
  email: string;
  password: string;
  phone: string;
  profileImageUrl: string | null;
  address: string;
  farmName: string | null;
  farmSize: number;
  farmSizeUnit: FarmSizeUnit;
  farmLocation: string | null;
}

export interface RegisterWorkerRequest {
  fullName: string;
  email: string;
  password: string;
  phone: string;
  profileImageUrl: string | null;
  address: string;
  experienceYears: number;
  expectedDailyWage: number;
}

export interface RegisterCustomerRequest {
  fullName: string;
  email: string;
  password: string;
  phone: string;
  profileImageUrl: string | null;
  address: string;
}

export interface AuthUser {
  userId: string;
  email: string;
  fullName: string;
  role: string;
  profileImageUrl?: string | null;
}

export interface LoginResponse {
  userId: string;
  email: string;
  fullName: string;
  role: string;
  expiresAt: string;
  message: string;
}

export interface FarmerRegistrationResponse {
  farmerId: string;
  email: string;
  fullName: string;
  message: string;
}

export interface WorkerRegistrationResponse {
  workerId: string;
  email: string;
  fullName: string;
  message: string;
}

export interface CustomerRegistrationResponse {
  customerId: string;
  email: string;
  fullName: string;
  message: string;
}
