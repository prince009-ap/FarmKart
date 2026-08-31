import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FarmerOrderService } from './farmer-order.service';
import { FarmerOrderDetail } from '../../core/models/farmer-order.models';

@Component({
  selector: 'app-farmer-order-detail',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './farmer-order-detail.component.html'
})
export class FarmerOrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly farmerOrderService = inject(FarmerOrderService);

  order = signal<FarmerOrderDetail | null>(null);
  isLoading = signal<boolean>(true);
  isUpdating = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  actionError = signal<string | null>(null);
  actionNote = signal<string>('');

  // Confirmation Modal Signals
  showConfirmModal = signal<boolean>(false);
  pendingTargetStatus = signal<string | null>(null);
  pendingStatusTitle = signal<string>('');
  pendingStatusPrompt = signal<string>('');
  pendingConfirmButtonText = signal<string>('');

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

    this.farmerOrderService.getFarmerOrderById(id).subscribe({
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

  openConfirmModal(targetStatus: string): void {
    const ord = this.order();
    if (!ord) return;

    this.pendingTargetStatus.set(targetStatus);
    this.actionError.set(null);

    if (targetStatus === 'PICKED_UP') {
      this.pendingStatusTitle.set('Confirm Handover');
      this.pendingStatusPrompt.set('Confirm that this order has been handed over to the customer?');
      this.pendingConfirmButtonText.set('Confirm Pickup');
    } else if (targetStatus === 'DISPATCHED') {
      this.pendingStatusTitle.set('Confirm Dispatch');
      this.pendingStatusPrompt.set('Confirm that this order has been dispatched?');
      this.pendingConfirmButtonText.set('Confirm Dispatch');
    } else if (targetStatus === 'READY_FOR_PICKUP') {
      this.pendingStatusTitle.set('Mark Order Ready');
      this.pendingStatusPrompt.set('Confirm that this order is ready for customer pickup/dispatch?');
      this.pendingConfirmButtonText.set('Confirm Ready');
    } else if (targetStatus === 'DELIVERED') {
      this.pendingStatusTitle.set('Mark Order Delivered');
      this.pendingStatusPrompt.set('Confirm that this order has been delivered to the customer?');
      this.pendingConfirmButtonText.set('Confirm Delivered');
    } else if (targetStatus === 'COMPLETED') {
      this.pendingStatusTitle.set('Complete Order');
      this.pendingStatusPrompt.set('Confirm that this order is completed and finalized?');
      this.pendingConfirmButtonText.set('Confirm Completed');
    } else {
      this.pendingStatusTitle.set('Confirm Action');
      this.pendingStatusPrompt.set('Confirm changing order status?');
      this.pendingConfirmButtonText.set('Confirm');
    }

    this.showConfirmModal.set(true);
  }

  cancelConfirmModal(): void {
    this.showConfirmModal.set(false);
    this.pendingTargetStatus.set(null);
  }

  confirmStatusUpdate(): void {
    const targetStatus = this.pendingTargetStatus();
    if (!targetStatus) return;

    this.showConfirmModal.set(false);
    this.updateStatus(targetStatus);
  }

  updateStatus(targetStatus: string): void {
    const ord = this.order();
    if (!ord) return;

    this.isUpdating.set(true);
    this.actionError.set(null);

    const note = this.actionNote().trim() || undefined;

    this.farmerOrderService.updateOrderStatus(ord.orderId, targetStatus, note).subscribe({
      next: () => {
        this.isUpdating.set(false);
        this.actionNote.set('');
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
