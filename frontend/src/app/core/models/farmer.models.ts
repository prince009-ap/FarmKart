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

export interface FarmerJobApplication {
  applicationId: string;
  jobId: string;
  jobTitle: string;
  applicantWorkerId: string;
  applicantName: string;
  applicantPhone: string;
  applicantExperienceYears: number;
  applicantSkills: string[];
  status: 'Pending' | 'Accepted' | 'Rejected' | 'Withdrawn';
  appliedAtUtc: string;
  message?: string;
}

export interface FarmerWorkerAssignment {
  assignmentId: string;
  jobId: string;
  jobTitle: string;
  workerProfileId: string;
  workerName: string;
  workerPhone: string;
  workerExperienceYears: number;
  workerSkills: string[];
  startDate: string;
  endDate: string | null;
  assignedAtUtc: string;
  status: 'Pending' | 'Active' | 'Completed' | 'Cancelled';
}

export type AttendanceStatus = 'Present' | 'Absent' | 'HalfDay' | 'Leave';

export interface MarkAttendanceItemRequest {
  workerAssignmentId: string;
  status: AttendanceStatus;
  notes?: string | null;
  checkIn?: string | null;
  checkOut?: string | null;
  totalHours?: number | null;
}

export interface SaveJobAttendanceRequest {
  date: string;
  items: MarkAttendanceItemRequest[];
}

export interface FarmerAttendanceRecord {
  attendanceId: string;
  workerAssignmentId: string;
  workerProfileId: string;
  workerName: string;
  workerPhone: string;
  date: string;
  status: AttendanceStatus;
  notes?: string | null;
  checkIn?: string | null;
  checkOut?: string | null;
  totalHours: number;
}
