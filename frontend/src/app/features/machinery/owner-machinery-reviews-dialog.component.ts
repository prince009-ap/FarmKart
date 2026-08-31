import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, Inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MachineryReviewService } from '../../core/services/machinery-review.service';
import { MachineryRatingSummaryResponse } from '../../core/models/machinery-review.models';

export interface OwnerMachineryReviewsDialogData {
  machineryId: string;
  machineryName: string;
}

@Component({
  selector: 'app-owner-machinery-reviews-dialog',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  template: `
    <div class="bg-slate-900 text-white p-6 rounded-2xl max-w-lg w-full space-y-5 font-sans border border-slate-800">

      <!-- Header -->
      <div class="flex items-center justify-between border-b border-slate-800 pb-3">
        <div>
          <h2 class="text-lg font-black tracking-tight flex items-center gap-2 text-amber-400">
            <span>🚜 {{ data.machineryName }}</span>
          </h2>
          <p class="text-xs text-slate-400">Received reviews for this machinery</p>
        </div>
        <button mat-icon-button (click)="close()" class="!text-slate-400 hover:!text-white">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="flex justify-center py-10">
          <mat-spinner diameter="36"></mat-spinner>
        </div>
      }

      <!-- Error State -->
      @if (!isLoading() && errorMessage()) {
        <div class="p-3 bg-red-950/60 border border-red-800 rounded-xl text-red-300 text-xs flex items-center gap-2">
          <mat-icon class="text-red-400 !w-4 !h-4 !text-sm">error</mat-icon>
          <span>{{ errorMessage() }}</span>
        </div>
      }

      <!-- Content -->
      @if (!isLoading() && summary()) {
        <!-- Summary Header Card -->
        <div class="bg-slate-950/70 border border-slate-800 rounded-xl p-4 flex items-center gap-6">
          <div class="text-center space-y-0.5">
            <p class="text-3xl font-black text-amber-400">{{ summary()!.averageRating.toFixed(1) }}</p>
            <div class="flex items-center justify-center gap-0.5">
              @for (star of [1,2,3,4,5]; track star) {
                <mat-icon class="!text-sm !w-3.5 !h-3.5" [class]="star <= summary()!.averageRating ? '!text-amber-400' : '!text-slate-700'">
                  {{ star <= summary()!.averageRating ? 'star' : 'star_border' }}
                </mat-icon>
              }
            </div>
            <p class="text-[10px] text-slate-500 font-bold uppercase">Avg Rating</p>
          </div>
          <div class="h-10 border-l border-slate-800"></div>
          <div>
            <p class="text-2xl font-black text-white">{{ summary()!.totalReviews }}</p>
            <p class="text-xs text-slate-400 font-semibold">Total Verified Reviews</p>
          </div>
        </div>

        <!-- Empty Reviews -->
        @if (summary()!.recentReviews.length === 0) {
          <div class="text-center py-8 space-y-2">
            <span class="text-4xl opacity-50">💬</span>
            <p class="text-slate-400 font-medium text-xs">No reviews received for this machinery yet.</p>
          </div>
        }

        <!-- Reviews List -->
        @if (summary()!.recentReviews.length > 0) {
          <div class="space-y-3 max-h-80 overflow-y-auto pr-1">
            @for (review of summary()!.recentReviews; track review.reviewId) {
              <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-3.5 space-y-2">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-xs text-slate-200">{{ review.reviewerName }}</span>
                  <div class="flex items-center gap-0.5">
                    @for (star of [1,2,3,4,5]; track star) {
                      <mat-icon class="!text-xs !w-3 !h-3" [class]="star <= review.rating ? '!text-amber-400' : '!text-slate-700'">
                        {{ star <= review.rating ? 'star' : 'star_border' }}
                      </mat-icon>
                    }
                  </div>
                </div>

                @if (review.comment) {
                  <p class="text-xs text-slate-300 italic bg-slate-900/80 p-2 rounded-lg border border-slate-800">
                    "{{ review.comment }}"
                  </p>
                }

                <p class="text-[10px] text-slate-500 text-right">{{ review.createdAtUtc | date:'mediumDate' }}</p>
              </div>
            }
          </div>
        }
      }

      <!-- Footer -->
      <div class="flex justify-end pt-2 border-t border-slate-800">
        <button mat-button (click)="close()" class="!text-slate-300 hover:!text-white text-xs">Close</button>
      </div>

    </div>
  `
})
export class OwnerMachineryReviewsDialogComponent implements OnInit {
  summary = signal<MachineryRatingSummaryResponse | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: OwnerMachineryReviewsDialogData,
    private dialogRef: MatDialogRef<OwnerMachineryReviewsDialogComponent>,
    private machineryReviewService: MachineryReviewService
  ) {}

  ngOnInit(): void {
    this.loadReviews();
  }

  loadReviews(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.machineryReviewService.getOwnerMachineryReviews(this.data.machineryId).subscribe({
      next: (res) => {
        this.summary.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to load machinery reviews.');
        this.isLoading.set(false);
      }
    });
  }

  close(): void {
    this.dialogRef.close();
  }
}
