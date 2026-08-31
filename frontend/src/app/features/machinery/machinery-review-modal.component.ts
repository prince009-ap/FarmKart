import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, Inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MachineryReviewService } from '../../core/services/machinery-review.service';
import { MachineryReviewResponse } from '../../core/models/machinery-review.models';

export interface MachineryReviewModalData {
  rentalId: string;
  machineryName: string;
  startDate: string;
  endDate: string;
  existingReview?: MachineryReviewResponse;
}

@Component({
  selector: 'app-machinery-review-modal',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatIconModule,
    MatButtonModule
  ],
  templateUrl: './machinery-review-modal.component.html'
})
export class MachineryReviewModalComponent {
  rating = signal<number>(5);
  hoverRating = signal<number>(0);
  comment = signal<string>('');
  submitting = signal<boolean>(false);
  errorMessage = signal<string | null>(null);

  constructor(
    public dialogRef: MatDialogRef<MachineryReviewModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: MachineryReviewModalData,
    private reviewService: MachineryReviewService,
    private snackBar: MatSnackBar
  ) {
    if (this.data.existingReview) {
      this.rating.set(this.data.existingReview.rating);
      this.comment.set(this.data.existingReview.comment || '');
    }
  }

  setRating(val: number): void {
    this.rating.set(val);
  }

  setHoverRating(val: number): void {
    this.hoverRating.set(val);
  }

  clearHover(): void {
    this.hoverRating.set(0);
  }

  submitReview(): void {
    const selectedRating = this.rating();
    if (selectedRating < 1 || selectedRating > 5) {
      this.errorMessage.set('Please select a rating between 1 and 5 stars.');
      return;
    }

    const commentText = this.comment().trim();
    if (commentText.length > 0 && (commentText.length < 5 || commentText.length > 1000)) {
      this.errorMessage.set('Comment must be between 5 and 1000 characters if provided.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (this.data.existingReview) {
      this.reviewService.updateReview(this.data.existingReview.reviewId, {
        rating: selectedRating,
        comment: commentText || undefined
      }).subscribe({
        next: (res) => {
          this.submitting.set(false);
          this.snackBar.open('Review updated successfully!', 'Close', { duration: 3000 });
          this.dialogRef.close(res);
        },
        error: (err) => {
          this.submitting.set(false);
          this.errorMessage.set(err?.error?.message || 'Failed to update review.');
        }
      });
    } else {
      this.reviewService.createRentalReview(this.data.rentalId, {
        rating: selectedRating,
        comment: commentText || undefined
      }).subscribe({
        next: (res) => {
          this.submitting.set(false);
          this.snackBar.open('Review submitted successfully!', 'Close', { duration: 3000 });
          this.dialogRef.close(res);
        },
        error: (err) => {
          this.submitting.set(false);
          this.errorMessage.set(err?.error?.message || 'Failed to submit review.');
        }
      });
    }
  }
}
