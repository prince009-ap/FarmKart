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
