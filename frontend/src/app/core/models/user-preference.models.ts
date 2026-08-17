export interface UserPreferenceResponse {
  theme: string;
  language: string;
  emailAlerts: boolean;
  smsAlerts: boolean;
  compactView: boolean;
}

export interface UpdateUserPreferenceRequest {
  theme: string;
  language: string;
  emailAlerts: boolean;
  smsAlerts: boolean;
  compactView: boolean;
}

export interface AccountSettingsResponse {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  phone: string;
}

export interface UpdateAccountProfileRequest {
  fullName: string;
  phone: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}
