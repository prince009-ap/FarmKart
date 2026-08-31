import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ReportService } from '../../core/services/report.service';
import { ReportTargetType } from '../../core/models/report.models';

@Component({
  selector: 'app-report-modal',
  standalone: true,
  imports: [
    TranslatePipe,CommonModule, FormsModule],
  template: `
    <div *ngIf="isOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-fade-in">
      <div class="bg-white rounded-2xl shadow-2xl max-w-lg w-full overflow-hidden border border-emerald-100">
        <!-- Header -->
        <div class="px-6 py-4 bg-gradient-to-r from-red-600 to-amber-600 text-white flex items-center justify-between">
          <div class="flex items-center gap-2">
            <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
            </svg>
            <h3 class="font-bold text-lg">Report Item or Content</h3>
          </div>
          <button (click)="close()" class="text-white/80 hover:text-white p-1 rounded-lg hover:bg-white/10 transition">
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Body -->
        <form (ngSubmit)="submitReport()" class="p-6 space-y-4">
          <div *ngIf="errorMessage" class="p-3 bg-red-50 text-red-700 text-sm rounded-xl border border-red-200">
            {{ errorMessage }}
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Target Entity</label>
            <div class="text-sm font-semibold text-slate-800 bg-slate-50 px-3 py-2 rounded-xl border border-slate-200">
              {{ targetType }} — {{ targetTitle || targetId }}
            </div>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Select Reason *</label>
            <select [(ngModel)]="reason" name="reason" required class="w-full text-sm rounded-xl border-slate-300 focus:border-red-500 focus:ring-red-500 p-2.5 bg-slate-50">
              <option value="" disabled>Choose a reason...</option>
              <option value="Fake or Misleading Listing">Fake or Misleading Listing</option>
              <option value="Inappropriate Content or Language">Inappropriate Content or Language</option>
              <option value="Unreasonable Price or Scam">Unreasonable Price or Scam</option>
              <option value="Damaged or Faulty Equipment">Damaged or Faulty Equipment</option>
              <option value="Spam or Fraudulent Activity">Spam or Fraudulent Activity</option>
              <option value="Other">Other Violation</option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Detailed Description *</label>
            <textarea [(ngModel)]="description" name="description" rows="4" required placeholder="Please provide specific details explaining the issue..." class="w-full text-sm rounded-xl border-slate-300 focus:border-red-500 focus:ring-red-500 p-3 bg-slate-50"></textarea>
          </div>

          <!-- Actions -->
          <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
            <button type="button" (click)="close()" class="px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-800 transition">{{ 'common.cancel' | translate }}</button>
            <button type="submit" [disabled]="isSubmitting || !reason || !description" class="px-5 py-2.5 text-sm font-semibold text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 rounded-xl shadow-lg shadow-red-600/20 transition flex items-center gap-2">
              <span *ngIf="isSubmitting" class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
              Submit Report
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ReportModalComponent {
  @Input() isOpen = false;
  @Input() targetType: ReportTargetType = 'Auction';
  @Input() targetId = '';
  @Input() targetTitle = '';

  @Output() isOpenChange = new EventEmitter<boolean>();
  @Output() reportSubmitted = new EventEmitter<void>();

  private readonly reportService = inject(ReportService);

  reason = '';
  description = '';
  errorMessage = '';
  isSubmitting = false;

  close(): void {
    this.isOpen = false;
    this.isOpenChange.emit(false);
    this.resetForm();
  }

  submitReport(): void {
    if (!this.reason || !this.description || !this.targetId) return;

    this.isSubmitting = true;
    this.errorMessage = '';

    this.reportService.createReport({
      targetType: this.targetType,
      targetId: this.targetId,
      reason: this.reason,
      description: this.description
    }).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.reportSubmitted.emit();
        this.close();
      },
      error: (err) => {
        this.isSubmitting = false;
        this.errorMessage = err.error?.message || 'Failed to submit report. Please try again.';
      }
    });
  }

  private resetForm(): void {
    this.reason = '';
    this.description = '';
    this.errorMessage = '';
  }
}
