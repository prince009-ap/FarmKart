export interface CustomerProfileResponse {
  customerProfileId: string;
  userId: string;
  fullName: string;
  email: string;
  phone: string;
  address: string;
  profileImageUrl: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateCustomerProfileRequest {
  fullName: string;
  phone: string;
  address: string;
}
