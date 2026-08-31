import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { DisputeService } from '../../core/services/dispute.service';
import { UserDisputeResponse } from '../../core/models/dispute.models';

@Component({
  selector: 'app-dispute-detail',
  standalone: true,
  imports: [
    TranslatePipe,CommonModule, FormsModule, RouterModule],
  template: `
    <div class="min-h-screen bg-slate-50 py-8 px-4 sm:px-6 lg:px-8">
      <div class="max-w-4xl mx-auto space-y-6">
        <!-- Back Navigation -->
        <button (click)="goBack()" class="inline-flex items-center gap-2 text-xs font-semibold text-slate-500 hover:text-slate-800 transition">
          ← {{ 'common.back' | translate }} to My Disputes
        </button>

        <div *ngIf="isLoading" class="bg-white rounded-2xl p-12 text-center text-slate-500 text-sm border border-slate-200/80">
          <span class="inline-block w-6 h-6 border-2 border-amber-600 border-t-transparent rounded-full animate-spin mb-2"></span>
          <p>Loading dispute details...</p>
        </div>

        <div *ngIf="!isLoading && dispute" class="space-y-6">
          <!-- Main Card -->
          <div class="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-6 space-y-6">
            <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 border-b border-slate-100 pb-4">
              <div>
                <span class="px-2.5 py-1 text-[11px] font-bold rounded-lg bg-amber-50 text-amber-800 uppercase tracking-wider">{{ dispute.relatedEntityType }}</span>
                <h1 class="text-2xl font-bold text-slate-900 mt-2">{{ dispute.entityTitle }}</h1>
                <p class="text-xs font-semibold text-amber-700 mt-0.5">Dispute Reason: {{ dispute.reason }}</p>
              </div>
              <div class="flex flex-col items-end gap-2">
                <span [ngClass]="getStatusBadgeClass(dispute.status)" class="px-4 py-1.5 text-xs font-bold rounded-full shadow-xs">
                  {{ dispute.status }}
                </span>
                <button *ngIf="dispute.status !== 'Closed' && dispute.status !== 'Resolved'" (click)="closeDispute()" class="text-xs font-semibold text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 px-3 py-1.5 rounded-lg transition">
                  Close Dispute
                </button>
              </div>
            </div>

            <!-- Description -->
            <div>
              <h4 class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">Issue Description & Remarks</h4>
              <div class="bg-slate-50 rounded-xl p-4 text-xs text-slate-700 leading-relaxed border border-slate-200/80">
                {{ dispute.description }}
              </div>
            </div>

            <!-- Resolution Note -->
            <div *ngIf="dispute.resolutionNote">
              <h4 class="text-xs font-semibold text-emerald-700 uppercase tracking-wider mb-2">Resolution / Closure Note</h4>
              <div class="bg-emerald-50 text-emerald-900 rounded-xl p-4 text-xs leading-relaxed border border-emerald-200">
                {{ dispute.resolutionNote }}
              </div>
            </div>
          </div>

          <!-- Timeline Card -->
          <div class="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-6 space-y-4">
            <h3 class="text-base font-bold text-slate-900">Dispute Activity Timeline</h3>

            <div class="relative pl-6 space-y-6 before:absolute before:left-2 before:top-2 before:bottom-2 before:w-0.5 before:bg-slate-200">
              <div *ngFor="let item of dispute.timeline" class="relative">
                <div class="absolute -left-[23px] top-1 w-3 h-3 rounded-full bg-amber-500 ring-4 ring-white"></div>
                <div class="space-y-0.5">
                  <div class="flex items-center gap-2">
                    <span class="text-xs font-bold text-slate-900">{{ item.status }}</span>
                    <span class="text-[11px] text-slate-400 font-medium">{{ item.timestampUtc | date:'medium' }}</span>
                  </div>
                  <p class="text-xs text-slate-600 leading-relaxed">{{ item.note }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class DisputeDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly disputeService = inject(DisputeService);

  dispute: UserDisputeResponse | null = null;
  isLoading = false;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadDispute(id);
    }
  }

  loadDispute(id: string): void {
    this.isLoading = true;
    this.disputeService.getDisputeById(id).subscribe({
      next: (res: UserDisputeResponse) => {
        this.dispute = res;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  closeDispute(): void {
    if (!this.dispute) return;
    const note = prompt('Please enter an optional resolution or closing note:');
    if (note !== null) {
      this.disputeService.closeDispute(this.dispute.id, note).subscribe({
        next: (updated: UserDisputeResponse) => {
          this.dispute = updated;
        }
      });
    }
  }

  goBack(): void {
    const currentUrl = this.router.url;
    let base = '/customer';
    if (currentUrl.includes('/farmer')) base = '/farmer';
    if (currentUrl.includes('/worker')) base = '/worker';
    this.router.navigate([base, 'my-disputes']);
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Open': return 'bg-amber-100 text-amber-800';
      case 'UnderReview': return 'bg-blue-100 text-blue-800';
      case 'Resolved': return 'bg-emerald-100 text-emerald-800';
      case 'Rejected': return 'bg-red-100 text-red-800';
      case 'Closed': return 'bg-slate-100 text-slate-800';
      default: return 'bg-slate-100 text-slate-800';
    }
  }
}
