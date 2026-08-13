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
