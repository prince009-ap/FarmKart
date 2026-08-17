export type DisputeEntityType = 'Order' | 'Payment' | 'AuctionAllocation' | 'MachineryRental';
export type DisputeStatus = 'Open' | 'UnderReview' | 'Resolved' | 'Rejected' | 'Closed';

export interface CreateDisputeRequest {
  relatedEntityType: DisputeEntityType;
  relatedEntityId: string;
  reason: string;
  description: string;
}

export interface DisputeTimelineItemDto {
  status: string;
  note: string;
  timestampUtc: string;
}

export interface UserDisputeResponse {
  id: string;
  raisedByUserId: string;
  relatedEntityType: DisputeEntityType;
  relatedEntityId: string;
  entityTitle: string;
  reason: string;
  description: string;
  status: DisputeStatus;
  resolutionNote?: string | null;
  timeline: DisputeTimelineItemDto[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface DisputeQueryRequest {
  status?: string;
  relatedEntityType?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedDisputeResponse {
  items: UserDisputeResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
