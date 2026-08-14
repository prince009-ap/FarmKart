export interface WorkerAvailableJob {
  id: string;
  title: string;
  description: string;
  workCategory: string;
  cropType?: string;
  workersRequired: number;
  requiredExperience: number;
  wagePerDay: number;
  startDate: string;
  endDate: string;
  workingHours: string;
  farmLocation: string;
  farmSize?: number;
  foodProvided: boolean;
  accommodationProvided: boolean;
  isUrgent: boolean;
  status: string;
  createdAtUtc: string;
  hasApplied: boolean;
  farmerName: string;
}

export interface ApplyJobRequest {
  message?: string;
}

export interface WorkerJobApplication {
  applicationId: string;
  jobId: string;
  jobTitle: string;
  workCategory: string;
  wagePerDay: number;
  startDate: string;
  endDate: string;
  farmLocation: string;
  status: 'Pending' | 'Accepted' | 'Rejected' | 'Withdrawn';
  appliedAtUtc: string;
  message?: string;
}

export interface WorkerAssignment {
  assignmentId: string;
  jobId: string;
  jobTitle: string;
  workCategory: string;
  wagePerDay: number;
  farmerName: string;
  farmLocation: string;
  workingHours: string;
  startDate: string;
  endDate: string | null;
  assignedAtUtc: string;
  status: 'Pending' | 'Active' | 'Completed' | 'Cancelled';
}

export interface WorkerAttendanceRecord {
  attendanceId: string;
  workerAssignmentId: string;
  jobId: string;
  jobTitle: string;
  farmerName?: string;
  date: string;
  status: 'Present' | 'Absent' | 'HalfDay' | 'Leave';
  notes?: string | null;
  totalHours?: number;
}

export interface WorkerAttendanceSummary {
  totalDays: number;
  presentDays: number;
  absentDays: number;
  halfDays: number;
  leaveDays: number;
  attendancePercentage: number;
  history: WorkerAttendanceRecord[];
}

export interface WorkerProfile {
  userId: string;
  fullName: string;
  email: string;
  phone: string;
  address: string;
  profileImageUrl?: string | null;
  experienceYears: number;
  expectedDailyWage: number;
  isAvailable: boolean;
  availableFrom?: string | null;
  availabilityNotes?: string | null;
  experienceDescription?: string | null;
  skills?: string[];
}

export interface WorkerProfileUpdateRequest {
  fullName: string;
  phone: string;
  address: string;
  experienceYears: number;
  expectedDailyWage: number;
  profileImageUrl?: string | null;
  isAvailable?: boolean;
  availableFrom?: string | null;
  availabilityNotes?: string | null;
  experienceDescription?: string | null;
  skills?: string[];
}
