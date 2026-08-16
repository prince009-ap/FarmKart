import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
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

  private get serverBaseUrl(): string {
    return environment.apiUrl.replace(/\/api\/?$/, '');
  }

  resolveImageUrl(url: string | null | undefined): string | null {
    if (!url) return null;
    if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) {
      return url;
    }
    const cleanPath = url.startsWith('/') ? url : `/${url}`;
    return `${this.serverBaseUrl}${cleanPath}`;
  }

  createOrderReview(orderId: string, request: CreateOrderReviewRequest): Observable<OrderReviewResponse> {
    return this.http.post<OrderReviewResponse>(`${this.apiUrl}/customer/orders/${orderId}/review`, request).pipe(
      map(res => ({ ...res, primaryImageUrl: this.resolveImageUrl(res.primaryImageUrl) }))
    );
  }

  getCustomerOrderReview(orderId: string): Observable<OrderReviewResponse> {
    return this.http.get<OrderReviewResponse>(`${this.apiUrl}/customer/orders/${orderId}/review`).pipe(
      map(res => ({ ...res, primaryImageUrl: this.resolveImageUrl(res.primaryImageUrl) }))
    );
  }

  updateOrderReview(orderId: string, request: UpdateOrderReviewRequest): Observable<OrderReviewResponse> {
    return this.http.put<OrderReviewResponse>(`${this.apiUrl}/customer/orders/${orderId}/review`, request).pipe(
      map(res => ({ ...res, primaryImageUrl: this.resolveImageUrl(res.primaryImageUrl) }))
    );
  }

  getMyCustomerReviews(): Observable<OrderReviewResponse[]> {
    return this.http.get<OrderReviewResponse[]>(`${this.apiUrl}/customer/reviews`).pipe(
      map(reviews => reviews.map(r => ({
        ...r,
        primaryImageUrl: this.resolveImageUrl(r.primaryImageUrl)
      })))
    );
  }

  getFarmerOrderReview(orderId: string): Observable<OrderReviewResponse> {
    return this.http.get<OrderReviewResponse>(`${this.apiUrl}/farmer/orders/${orderId}/review`).pipe(
      map(res => ({ ...res, primaryImageUrl: this.resolveImageUrl(res.primaryImageUrl) }))
    );
  }

  getFarmerRatingSummary(): Observable<FarmerRatingSummaryResponse> {
    return this.http.get<FarmerRatingSummaryResponse>(`${this.apiUrl}/farmer/reviews`);
  }
}
