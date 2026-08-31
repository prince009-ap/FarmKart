import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, Inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { OrderReviewService } from '../../core/services/order-review.service';
import { OrderReviewResponse } from '../../core/models/order-review.models';

export interface OrderReviewDialogData {
  orderId: string;
  orderNumber: string;
  farmerName: string;
  cropName: string;
  existingReview?: OrderReviewResponse | null;
}

@Component({
  selector: 'app-order-review-dialog',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './order-review-dialog.component.html'
})
export class OrderReviewDialogComponent {
  rating = signal<number>(5);
  hoverRating = signal<number>(0);
  comment = signal<string>('');

  isSubmitting = signal<boolean>(false);
  errorMessage = signal<string | null>(null);

  isEditing = signal<boolean>(false);

  constructor(
    public dialogRef: MatDialogRef<OrderReviewDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: OrderReviewDialogData,
    private orderReviewService: OrderReviewService
  ) {
    if (data.existingReview) {
      this.isEditing.set(true);
      this.rating.set(data.existingReview.rating);
      this.comment.set(data.existingReview.comment || '');
    }
  }

  setRating(val: number): void {
    this.rating.set(val);
  }

  setHover(val: number): void {
    this.hoverRating.set(val);
  }

  submitReview(): void {
    this.errorMessage.set(null);

    const r = this.rating();
    if (r < 1 || r > 5) {
      this.errorMessage.set('Please select a star rating between 1 and 5.');
      return;
    }

    const c = this.comment().trim();
    if (c.length > 0 && (c.length < 5 || c.length > 1000)) {
      this.errorMessage.set('Review text must be between 5 and 1000 characters.');
      return;
    }

    this.isSubmitting.set(true);

    if (this.isEditing()) {
      this.orderReviewService.updateOrderReview(this.data.orderId, { rating: r, comment: c || undefined }).subscribe({
        next: (res) => {
          this.isSubmitting.set(false);
          this.dialogRef.close(res);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.errorMessage.set(err.error?.message || 'Failed to update review.');
        }
      });
    } else {
      this.orderReviewService.createOrderReview(this.data.orderId, { rating: r, comment: c || undefined }).subscribe({
        next: (res) => {
          this.isSubmitting.set(false);
          this.dialogRef.close(res);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.errorMessage.set(err.error?.message || 'Failed to submit review.');
        }
      });
    }
  }

  close(): void {
    this.dialogRef.close();
  }
}
