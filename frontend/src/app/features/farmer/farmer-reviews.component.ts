import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { OrderReviewService } from '../../core/services/order-review.service';
import { FarmerRatingSummaryResponse, OrderReviewResponse } from '../../core/models/order-review.models';

@Component({
  selector: 'app-farmer-reviews',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    RouterLink
  ],
  templateUrl: './farmer-reviews.component.html'
})
export class FarmerReviewsComponent implements OnInit {
  summary = signal<FarmerRatingSummaryResponse | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  constructor(private orderReviewService: OrderReviewService) {}

  ngOnInit(): void {
    this.loadReviews();
  }

  loadReviews(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.orderReviewService.getFarmerRatingSummary().subscribe({
      next: (res) => {
        this.summary.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to load reviews.');
        this.isLoading.set(false);
      }
    });
  }

  stars(n: number): number[] {
    return Array.from({ length: n }, (_, i) => i + 1);
  }
}
