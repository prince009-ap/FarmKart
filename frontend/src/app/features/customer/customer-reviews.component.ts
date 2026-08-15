import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import { OrderReviewService } from '../../core/services/order-review.service';
import { OrderReviewResponse } from '../../core/models/order-review.models';
import { OrderReviewDialogComponent } from './order-review-dialog.component';

@Component({
  selector: 'app-customer-reviews',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    RouterLink
  ],
  templateUrl: './customer-reviews.component.html'
})
export class CustomerReviewsComponent implements OnInit {
  reviews = signal<OrderReviewResponse[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  constructor(
    private orderReviewService: OrderReviewService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.loadReviews();
  }

  loadReviews(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.orderReviewService.getMyCustomerReviews().subscribe({
      next: (res) => {
        this.reviews.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to load your review history.');
        this.isLoading.set(false);
      }
    });
  }

  editReview(review: OrderReviewResponse): void {
    const dialogRef = this.dialog.open(OrderReviewDialogComponent, {
      width: '480px',
      data: {
        orderId: review.orderId,
        orderNumber: review.orderNumber,
        farmerName: review.farmerName,
        cropName: review.cropName,
        existingReview: review
      }
    });

    dialogRef.afterClosed().subscribe((updatedReview: OrderReviewResponse | undefined) => {
      if (updatedReview) {
        this.loadReviews();
      }
    });
  }
}
