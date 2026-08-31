import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DisputeService } from '../../core/services/dispute.service';
import { DisputeEntityType } from '../../core/models/dispute.models';

@Component({
  selector: 'app-dispute-modal',
  standalone: true,
  imports: [
    TranslatePipe,CommonModule, FormsModule],
  template: `
    <div *ngIf="isOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-fade-in">
      <div class="bg-white rounded-2xl shadow-2xl max-w-lg w-full overflow-hidden border border-amber-100">
        <!-- Header -->
        <div class="px-6 py-4 bg-gradient-to-r from-amber-600 to-orange-600 text-white flex items-center justify-between">
          <div class="flex items-center gap-2">
            <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
            <h3 class="font-bold text-lg">Raise Transaction Dispute</h3>
          </div>
          <button (click)="close()" class="text-white/80 hover:text-white p-1 rounded-lg hover:bg-white/10 transition">
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Body -->
        <form (ngSubmit)="submitDispute()" class="p-6 space-y-4">
          <div *ngIf="errorMessage" class="p-3 bg-red-50 text-red-700 text-sm rounded-xl border border-red-200">
            {{ errorMessage }}
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Related Transaction</label>
            <div class="text-sm font-semibold text-slate-800 bg-slate-50 px-3 py-2 rounded-xl border border-slate-200">
              {{ relatedEntityType }} — {{ entityTitle || relatedEntityId }}
            </div>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Dispute Reason *</label>
            <select [(ngModel)]="reason" name="reason" required class="w-full text-sm rounded-xl border-slate-300 focus:border-amber-500 focus:ring-amber-500 p-2.5 bg-slate-50">
              <option value="" disabled>Choose a dispute reason...</option>
              <option value="Incorrect Quantity Received">Incorrect Quantity Received</option>
              <option value="Poor Quality or Damaged Goods">Poor Quality or Damaged Goods</option>
              <option value="Delayed Delivery or Pickup Issue">Delayed Delivery or Pickup Issue</option>
              <option value="Payment Deduction Discrepancy">Payment Deduction Discrepancy</option>
              <option value="Machinery Condition or Breakdown">Machinery Condition or Breakdown</option>
              <option value="Unfulfilled Agreement Terms">Unfulfilled Agreement Terms</option>
              <option value="Other">Other Dispute Reason</option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Description & Desired Resolution *</label>
            <textarea [(ngModel)]="description" name="description" rows="4" required placeholder="Describe the transaction issue clearly and what resolution you expect..." class="w-full text-sm rounded-xl border-slate-300 focus:border-amber-500 focus:ring-amber-500 p-3 bg-slate-50"></textarea>
          </div>

          <!-- Actions -->
          <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
            <button type="button" (click)="close()" class="px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-800 transition">{{ 'common.cancel' | translate }}</button>
            <button type="submit" [disabled]="isSubmitting || !reason || !description" class="px-5 py-2.5 text-sm font-semibold text-white bg-amber-600 hover:bg-amber-700 disabled:opacity-50 rounded-xl shadow-lg shadow-amber-600/20 transition flex items-center gap-2">
              <span *ngIf="isSubmitting" class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
              Raise Dispute
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class DisputeModalComponent {
  @Input() isOpen = false;
  @Input() relatedEntityType: DisputeEntityType = 'Order';
  @Input() relatedEntityId = '';
  @Input() entityTitle = '';

  @Output() isOpenChange = new EventEmitter<boolean>();
  @Output() disputeSubmitted = new EventEmitter<void>();

  private readonly disputeService = inject(DisputeService);

  reason = '';
  description = '';
  errorMessage = '';
  isSubmitting = false;

  close(): void {
    this.isOpen = false;
    this.isOpenChange.emit(false);
    this.resetForm();
  }

  submitDispute(): void {
    if (!this.reason || !this.description || !this.relatedEntityId) return;

    this.isSubmitting = true;
    this.errorMessage = '';

    this.disputeService.createDispute({
      relatedEntityType: this.relatedEntityType,
      relatedEntityId: this.relatedEntityId,
      reason: this.reason,
      description: this.description
    }).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.disputeSubmitted.emit();
        this.close();
      },
      error: (err) => {
        this.isSubmitting = false;
        this.errorMessage = err.error?.message || 'Failed to raise dispute. Please try again.';
      }
    });
  }

  private resetForm(): void {
    this.reason = '';
    this.description = '';
    this.errorMessage = '';
  }
}
