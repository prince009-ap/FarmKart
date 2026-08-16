export interface CreateOrderReviewRequest {
  rating: number;
  comment?: string;
}

export interface UpdateOrderReviewRequest {
  rating: number;
  comment?: string;
}

export interface OrderReviewResponse {
  reviewId: string;
  orderId: string;
  orderNumber: string;
  customerName: string;
  farmerName: string;
  cropName: string;
  cropType?: string;
  primaryImageUrl?: string | null;
  rating: number;
  comment?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
}

export interface FarmerRatingSummaryResponse {
  averageRating: number;
  totalReviews: number;
  recentReviews: OrderReviewResponse[];
}

export interface UnifiedReviewItemResponse {
  reviewId: string;
  reviewType: 'CROP' | 'MACHINERY';
  rating: number;
  comment?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  orderId?: string;
  orderNumber?: string;
  cropName?: string;
  cropType?: string;
  rentalId?: string;
  rentalNumber?: string;
  machineryId?: string;
  machineryName?: string;
  targetName?: string;
  primaryImageUrl?: string | null;
  canEdit: boolean;
}

export interface UserMyReviewsSummaryResponse {
  totalCount: number;
  cropCount: number;
  machineryCount: number;
  allReviews: UnifiedReviewItemResponse[];
  cropReviews: UnifiedReviewItemResponse[];
  machineryReviews: UnifiedReviewItemResponse[];
}
