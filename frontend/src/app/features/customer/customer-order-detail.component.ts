import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerOrderService } from './customer-order.service';
import { CustomerOrderDetail } from '../../core/models/customer-auction.models';

@Component({
  selector: 'app-customer-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './customer-order-detail.component.html'
})
export class CustomerOrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly orderService = inject(CustomerOrderService);

  order = signal<CustomerOrderDetail | null>(null);
  isLoading = signal<boolean>(true);
  isUpdating = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  actionError = signal<string | null>(null);

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
