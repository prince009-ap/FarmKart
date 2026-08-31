import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { CustomerOrderService } from './customer-order.service';
import { CustomerOrderDetail } from '../../core/models/customer-auction.models';
import { OrderReviewService } from '../../core/services/order-review.service';
import { OrderReviewResponse } from '../../core/models/order-review.models';
import { OrderReviewDialogComponent } from './order-review-dialog.component';

@Component({
  selector: 'app-customer-order-detail',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatDialogModule
  ],
  templateUrl: './customer-order-detail.component.html'
})
export class CustomerOrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly orderService = inject(CustomerOrderService);
  private readonly reviewService = inject(OrderReviewService);
  private readonly dialog = inject(MatDialog);

  order = signal<CustomerOrderDetail | null>(null);
  isLoading = signal<boolean>(true);
  isUpdating = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  actionError = signal<string | null>(null);

  existingReview = signal<OrderReviewResponse | null>(null);
  reviewLoading = signal<boolean>(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadOrderDetail(id);
    } else {
      this.errorMessage.set('Invalid order ID.');
      this.isLoading.set(false);
    }
  }

  loadOrderDetail(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.orderService.getCustomerOrderById(id).subscribe({
      next: (data) => {
        this.order.set(data);
        this.isLoading.set(false);

        if (data.status === 'COMPLETED' && data.paymentStatus === 'PAID') {
          this.loadExistingReview(data.orderId);
        }
      },
      error: (err) => {
        if (err?.status === 404 || err?.status === 403) {
          this.errorMessage.set('Order not found or you do not have permission to view this order.');
        } else {
          this.errorMessage.set('Unable to load order details.');
        }
        this.isLoading.set(false);
      }
    });
  }

  loadExistingReview(orderId: string): void {
    this.reviewLoading.set(true);
    this.reviewService.getCustomerOrderReview(orderId).subscribe({
      next: (r) => {
        this.existingReview.set(r);
        this.reviewLoading.set(false);
      },
      error: () => {
        // 404 means no review submitted yet — that is expected
        this.existingReview.set(null);
        this.reviewLoading.set(false);
      }
    });
  }

  openReviewDialog(): void {
    const o = this.order();
    if (!o) return;

    const dialogRef = this.dialog.open(OrderReviewDialogComponent, {
      width: '480px',
      data: {
        orderId: o.orderId,
        orderNumber: o.orderNumber,
        farmerName: o.farmerName,
        cropName: o.cropName,
        existingReview: this.existingReview()
      }
    });

    dialogRef.afterClosed().subscribe((result: OrderReviewResponse | undefined) => {
      if (result) {
        this.existingReview.set(result);
      }
    });
  }

  markCompleted(): void {
    const ord = this.order();
    if (!ord) return;

    this.isUpdating.set(true);
    this.actionError.set(null);

    this.orderService.updateOrderStatus(ord.orderId, 'COMPLETED', 'Customer confirmed order receipt and completion.').subscribe({
      next: () => {
        this.isUpdating.set(false);
        this.loadOrderDetail(ord.orderId);
      },
      error: (err) => {
        this.isUpdating.set(false);
        this.actionError.set(err?.error?.message || 'Failed to update order status.');
      }
    });
  }

  onImageError(): void {
    const current = this.order();
    if (current) {
      this.order.set({ ...current, primaryImageUrl: null });
    }
  }
}
