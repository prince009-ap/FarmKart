import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateMachineryReviewRequest,
  UpdateMachineryReviewRequest,
  MachineryReviewResponse,
  MachineryRatingSummaryResponse
} from '../models/machinery-review.models';

@Injectable({
  providedIn: 'root'
})
export class MachineryReviewService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl;

  createRentalReview(rentalId: string, req: CreateMachineryReviewRequest): Observable<MachineryReviewResponse> {
    return this.http.post<MachineryReviewResponse>(`${this.apiUrl}/rentals/${rentalId}/review`, req);
  }

  getRentalReview(rentalId: string): Observable<MachineryReviewResponse> {
    return this.http.get<MachineryReviewResponse>(`${this.apiUrl}/rentals/${rentalId}/review`);
  }

  getMachineryReviews(machineryId: string): Observable<MachineryRatingSummaryResponse> {
    return this.http.get<MachineryRatingSummaryResponse>(`${this.apiUrl}/machinery/${machineryId}/reviews`);
  }

  getOwnerMachineryReviews(machineryId: string): Observable<MachineryRatingSummaryResponse> {
    return this.http.get<MachineryRatingSummaryResponse>(`${this.apiUrl}/my-machinery/${machineryId}/reviews`);
  }

  updateReview(reviewId: string, req: UpdateMachineryReviewRequest): Observable<MachineryReviewResponse> {
    return this.http.put<MachineryReviewResponse>(`${this.apiUrl}/reviews/${reviewId}`, req);
  }
}
