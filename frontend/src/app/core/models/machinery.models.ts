export type MachineryAvailabilityStatus = 'Available' | 'Reserved' | 'Rented' | 'Maintenance' | 'Unavailable';

export type MachineryRentalStatus = 'Booked' | 'Confirmed' | 'ReadyForHandover' | 'RentedOut' | 'Returned' | 'Completed' | 'Cancelled';

export interface MachineryImageResponse {
  id: string;
  machineryId: string;
  imageUrl: string;
  isPrimary: boolean;
  displayOrder: number;
  createdAtUtc: string;
}

export interface MachineryResponse {
  id: string;
  ownerUserId: string;
  ownerName: string;
  name: string;
  category: string;
  brand?: string;
  model?: string;
  manufacturingYear?: number;
  description?: string;
  dailyRent: number;
  securityDeposit: number;
  isDriverIncluded: boolean;
  isFuelIncluded: boolean;
  driverAvailable: boolean;
  driverChargePerDay: number;
  driverName?: string;
  driverPhone?: string;
  driverNotes?: string;
  availabilityStatus: MachineryAvailabilityStatus;
  location: string;
  city?: string;
  state?: string;
  pincode?: string;
  isActive: boolean;
  isFavorited: boolean;
  isOwnedByCurrentUser?: boolean;
  images: MachineryImageResponse[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PagedMachineryResponse {
  items: MachineryResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface MachineryFilterRequest {
  search?: string;
  name?: string;
  category?: string;
  brand?: string;
  city?: string;
  state?: string;
  location?: string;
  minRentPerDay?: number;
  maxRentPerDay?: number;
  driverAvailable?: boolean;
  isDriverIncluded?: boolean;
  startDate?: string;
  endDate?: string;
  sortBy?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateMachineryRequest {
  name: string;
  category: string;
  brand?: string;
  model?: string;
  manufacturingYear?: number;
  description?: string;
  dailyRent: number;
  securityDeposit: number;
  isDriverIncluded: boolean;
  isFuelIncluded: boolean;
  driverAvailable: boolean;
  driverChargePerDay: number;
  driverName?: string;
  driverPhone?: string;
  driverNotes?: string;
  location: string;
  city?: string;
  state?: string;
  pincode?: string;
}

export interface UpdateMachineryRequest {
  name?: string;
  category?: string;
  brand?: string;
  model?: string;
  manufacturingYear?: number;
  description?: string;
  dailyRent?: number;
  securityDeposit?: number;
  isDriverIncluded?: boolean;
  isFuelIncluded?: boolean;
  driverAvailable?: boolean;
  driverChargePerDay?: number;
  driverName?: string;
  driverPhone?: string;
  driverNotes?: string;
  location?: string;
  city?: string;
  state?: string;
  pincode?: string;
  availabilityStatus?: MachineryAvailabilityStatus;
}

export interface BookRentalRequest {
  startDate: string; // YYYY-MM-DD
  endDate: string;   // YYYY-MM-DD
  driverRequired: boolean;
  paymentMethod: string;
}

export interface MachineryRentalResponse {
  id: string;
  machineryId: string;
  machineryName: string;
  machineryCategory: string;
  machineryPrimaryImageUrl?: string;
  ownerUserId: string;
  ownerName: string;
  renterUserId: string;
  renterName: string;
  startDate: string;
  endDate: string;
  rentalDays: number;
  rentPerDaySnapshot: number;
  driverChargePerDaySnapshot: number;
  driverRequired: boolean;
  machineryAmount: number;
  driverAmount: number;
  totalAmount: number;
  securityDepositSnapshot: number;
  totalRentAmount: number;
  totalPayableAmount: number;
  paymentStatus: string;
  paymentTransactionRef?: string;
  paymentMethod?: string;
  rentalStatus: MachineryRentalStatus;
  returnedAtUtc?: string;
  completedAtUtc?: string;
  cancellationReason?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateRentalStatusRequest {
  newStatus: MachineryRentalStatus;
  cancellationReason?: string;
}

export interface RentalDateRange {
  startDate: string;
  endDate: string;
}

export interface MachineryAvailabilityResponse {
  machineryId: string;
  bookedRanges: RentalDateRange[];
}

export const MACHINERY_CATEGORIES = [
  'Tractor',
  'Harvester',
  'Rotavator',
  'Cultivator',
  'Plough',
  'Seed Drill',
  'Sprayer',
  'JCB',
  'Other'
];
