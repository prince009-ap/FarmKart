import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import { OrderReviewService } from '../../core/services/order-review.service';
import { UserMyReviewsSummaryResponse, UnifiedReviewItemResponse, OrderReviewResponse } from '../../core/models/order-review.models';
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
  summary = signal<UserMyReviewsSummaryResponse | null>(null);
  selectedTab = signal<'ALL' | 'CROP' | 'MACHINERY'>('ALL');
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  filteredReviews = computed(() => {
    const s = this.summary();
    if (!s) return [];
    const tab = this.selectedTab();
    if (tab === 'CROP') return s.cropReviews;
    if (tab === 'MACHINERY') return s.machineryReviews;
    return s.allReviews;
  });

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

    this.orderReviewService.getUnifiedMyReviews().subscribe({
      next: (res) => {
        this.summary.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to load your review history.');
        this.isLoading.set(false);
      }
    });
  }

  setTab(tab: 'ALL' | 'CROP' | 'MACHINERY'): void {
    this.selectedTab.set(tab);
  }

  editReview(review: UnifiedReviewItemResponse): void {
    if (!review.canEdit || review.reviewType !== 'CROP' || !review.orderId) return;

    const dialogRef = this.dialog.open(OrderReviewDialogComponent, {
      width: '480px',
      data: {
        orderId: review.orderId,
        orderNumber: review.orderNumber || '',
        farmerName: review.targetName || '',
        cropName: review.cropName || '',
        existingReview: {
          reviewId: review.reviewId,
          orderId: review.orderId,
          orderNumber: review.orderNumber || '',
          customerName: '',
          farmerName: review.targetName || '',
          cropName: review.cropName || '',
          rating: review.rating,
          comment: review.comment,
          createdAtUtc: review.createdAtUtc
        } as OrderReviewResponse
      }
    });

    dialogRef.afterClosed().subscribe((updated: any) => {
      if (updated) {
        this.loadReviews();
      }
    });
  }
}
