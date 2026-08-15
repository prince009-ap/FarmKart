import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateOrderReviewRequest,
  UpdateOrderReviewRequest,
  OrderReviewResponse,
  FarmerRatingSummaryResponse
} from '../models/order-review.models';

@Injectable({
  providedIn: 'root'
})
export class OrderReviewService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl;

  createOrderReview(orderId: string, request: CreateOrderReviewRequest): Observable<OrderReviewResponse> {
    return this.http.post<OrderReviewResponse>(`${this.apiUrl}/customer/orders/${orderId}/review`, request);
  }

  getCustomerOrderReview(orderId: string): Observable<OrderReviewResponse> {
    return this.http.get<OrderReviewResponse>(`${this.apiUrl}/customer/orders/${orderId}/review`);
  }

  updateOrderReview(orderId: string, request: UpdateOrderReviewRequest): Observable<OrderReviewResponse> {
    return this.http.put<OrderReviewResponse>(`${this.apiUrl}/customer/orders/${orderId}/review`, request);
  }

  getMyCustomerReviews(): Observable<OrderReviewResponse[]> {
    return this.http.get<OrderReviewResponse[]>(`${this.apiUrl}/customer/reviews`);
  }

  getFarmerOrderReview(orderId: string): Observable<OrderReviewResponse> {
    return this.http.get<OrderReviewResponse>(`${this.apiUrl}/farmer/orders/${orderId}/review`);
  }

  getFarmerRatingSummary(): Observable<FarmerRatingSummaryResponse> {
    return this.http.get<FarmerRatingSummaryResponse>(`${this.apiUrl}/farmer/reviews`);
  }
}
