import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerOrderService } from './customer-order.service';
import { CustomerOrderTracking } from '../../core/models/customer-auction.models';

@Component({
  selector: 'app-customer-order-tracking',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './customer-order-tracking.component.html'
})
export class CustomerOrderTrackingComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly customerOrderService = inject(CustomerOrderService);

  tracking = signal<CustomerOrderTracking | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadTracking(id);
    } else {
      this.errorMessage.set('Invalid order ID.');
      this.isLoading.set(false);
    }
  }

  loadTracking(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.customerOrderService.getOrderTracking(id).subscribe({
      next: (data) => {
        this.tracking.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        if (err?.status === 404 || err?.status === 403) {
          this.errorMessage.set('Order not found or you do not have permission to track this order.');
        } else {
          this.errorMessage.set('Unable to load order tracking details.');
        }
        this.isLoading.set(false);
      }
    });
  }

  isStepCompleted(stepStatus: string): boolean {
    const tr = this.tracking();
    if (!tr) return false;
    const current = tr.currentStatus;

    const orderSeq = ['CONFIRMED', 'READY_FOR_PICKUP', 'DISPATCHED', 'PICKED_UP', 'DELIVERED', 'COMPLETED'];
    const stepIdx = orderSeq.indexOf(stepStatus);
    const currIdx = orderSeq.indexOf(current);

    if (stepIdx === -1 || currIdx === -1) return false;
    return stepIdx <= currIdx;
  }

  isStepCurrent(stepStatus: string): boolean {
    const tr = this.tracking();
    if (!tr) return false;
    return tr.currentStatus === stepStatus;
  }

  getStepTimestamp(stepStatus: string): string | null {
    const tr = this.tracking();
    if (!tr || !tr.statusHistory) return null;

    const event = tr.statusHistory.find(h => h.newStatus === stepStatus);
    return event ? event.changedAtUtc : null;
  }

  onImageError(): void {
    const current = this.tracking();
    if (current) {
      this.tracking.set({ ...current, primaryImageUrl: null });
    }
  }
}
