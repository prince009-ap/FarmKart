export type ReportTargetType = 'Auction' | 'Machinery' | 'Review' | 'User';
export type ReportStatus = 'Open' | 'UnderReview' | 'Resolved' | 'Rejected' | 'Closed';

export interface CreateReportRequest {
  targetType: ReportTargetType;
  targetId: string;
  reason: string;
  description: string;
}

export interface UserReportResponse {
  id: string;
  reporterUserId: string;
  targetType: ReportTargetType;
  targetId: string;
  targetTitle: string;
  reason: string;
  description: string;
  status: ReportStatus;
  resolutionNote?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ReportQueryRequest {
  status?: string;
  targetType?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedReportResponse {
  items: UserReportResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
