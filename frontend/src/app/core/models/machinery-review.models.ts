export interface CreateMachineryReviewRequest {
  rating: number;
  comment?: string;
}

export interface UpdateMachineryReviewRequest {
  rating: number;
  comment?: string;
}

export interface MachineryReviewResponse {
  reviewId: string;
  rentalId: string;
  machineryId: string;
  machineryName: string;
  reviewerName: string;
  rating: number;
  comment?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
}

export interface MachineryRatingSummaryResponse {
  averageRating: number;
  totalReviews: number;
  recentReviews: MachineryReviewResponse[];
}
