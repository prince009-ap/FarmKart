import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ReportService } from '../../core/services/report.service';
import { UserReportResponse, PagedReportResponse } from '../../core/models/report.models';

@Component({
  selector: 'app-my-reports',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-50 py-8 px-4 sm:px-6 lg:px-8">
      <div class="max-w-5xl mx-auto space-y-6">
        <!-- Header -->
        <div class="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-6 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
          <div>
            <h1 class="text-2xl font-bold text-slate-900">My Reports</h1>
            <p class="text-xs text-slate-500">Track all your submitted reports regarding listings, machinery, or user reviews.</p>
          </div>
        </div>

        <!-- Filter Bar -->
        <div class="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-4 flex flex-wrap items-center justify-between gap-4">
          <div class="flex bg-slate-100 p-1 rounded-xl">
            <button (click)="setStatus('')" [class.bg-white]="selectedStatus === ''" [class.shadow-sm]="selectedStatus === ''" class="px-4 py-1.5 text-xs font-semibold rounded-lg transition text-slate-700">All</button>
            <button (click)="setStatus('Open')" [class.bg-white]="selectedStatus === 'Open'" [class.shadow-sm]="selectedStatus === 'Open'" class="px-4 py-1.5 text-xs font-semibold rounded-lg transition text-slate-700">Open</button>
            <button (click)="setStatus('UnderReview')" [class.bg-white]="selectedStatus === 'UnderReview'" [class.shadow-sm]="selectedStatus === 'UnderReview'" class="px-4 py-1.5 text-xs font-semibold rounded-lg transition text-slate-700">Under Review</button>
            <button (click)="setStatus('Resolved')" [class.bg-white]="selectedStatus === 'Resolved'" [class.shadow-sm]="selectedStatus === 'Resolved'" class="px-4 py-1.5 text-xs font-semibold rounded-lg transition text-slate-700">Resolved</button>
            <button (click)="setStatus('Rejected')" [class.bg-white]="selectedStatus === 'Rejected'" [class.shadow-sm]="selectedStatus === 'Rejected'" class="px-4 py-1.5 text-xs font-semibold rounded-lg transition text-slate-700">Rejected</button>
          </div>

          <div class="relative w-full sm:w-64">
            <input type="text" [(ngModel)]="searchTerm" (keyup.enter)="loadReports()" placeholder="Search reports..." class="w-full text-xs rounded-xl border-slate-300 focus:border-red-500 focus:ring-red-500 pl-8 pr-3 py-2 bg-slate-50" />
            <svg class="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </div>
        </div>

        <!-- Reports List -->
        <div class="space-y-4">
          <div *ngIf="isLoading" class="bg-white rounded-2xl p-12 text-center text-slate-500 text-sm border border-slate-200/80">
            <span class="inline-block w-6 h-6 border-2 border-red-600 border-t-transparent rounded-full animate-spin mb-2"></span>
            <p>Loading reports...</p>
          </div>

          <div *ngIf="!isLoading && reports.length === 0" class="bg-white rounded-2xl p-12 text-center border border-slate-200/80">
            <div class="w-12 h-12 bg-slate-100 rounded-full flex items-center justify-center mx-auto mb-3 text-slate-400 text-xl">📋</div>
            <h3 class="text-base font-semibold text-slate-800">No Reports Found</h3>
            <p class="text-xs text-slate-500 mt-1">You have not submitted any reports matching the selected criteria.</p>
          </div>

          <div *ngFor="let r of reports" class="bg-white rounded-2xl p-6 shadow-sm border border-slate-200/80 space-y-4">
            <div class="flex items-start justify-between gap-4">
              <div>
                <span class="px-2.5 py-1 text-[11px] font-bold rounded-lg bg-slate-100 text-slate-700 uppercase tracking-wider">{{ r.targetType }}</span>
                <h3 class="text-base font-bold text-slate-900 mt-2">{{ r.targetTitle }}</h3>
                <p class="text-xs font-semibold text-red-600 mt-0.5">Reason: {{ r.reason }}</p>
              </div>
              <span [ngClass]="getStatusBadgeClass(r.status)" class="px-3 py-1 text-xs font-bold rounded-full">
                {{ r.status }}
              </span>
            </div>

            <div class="bg-slate-50 rounded-xl p-4 text-xs text-slate-700 leading-relaxed border border-slate-100">
              <span class="font-bold text-slate-900 block mb-1">Description:</span>
              {{ r.description }}
            </div>

            <div *ngIf="r.resolutionNote" class="bg-emerald-50 text-emerald-900 rounded-xl p-4 text-xs leading-relaxed border border-emerald-200">
              <span class="font-bold block mb-1">Resolution Note:</span>
              {{ r.resolutionNote }}
            </div>

            <div class="text-[11px] text-slate-400 font-medium pt-2 border-t border-slate-100 flex items-center justify-between">
              <span>Submitted: {{ r.createdAtUtc | date:'medium' }}</span>
              <span>Report ID: #{{ r.id.substring(0, 8) }}</span>
            </div>
          </div>
        </div>

        <!-- Pagination -->
        <div *ngIf="totalPages > 1" class="flex items-center justify-between bg-white rounded-2xl p-4 border border-slate-200/80">
          <p class="text-xs text-slate-500">Page {{ currentPage }} of {{ totalPages }}</p>
          <div class="flex gap-2">
            <button (click)="changePage(currentPage - 1)" [disabled]="currentPage === 1" class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 hover:bg-slate-200 disabled:opacity-40 transition">Previous</button>
            <button (click)="changePage(currentPage + 1)" [disabled]="currentPage === totalPages" class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 hover:bg-slate-200 disabled:opacity-40 transition">Next</button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class MyReportsComponent implements OnInit {
  private readonly reportService = inject(ReportService);

  reports: UserReportResponse[] = [];
  selectedStatus = '';
  searchTerm = '';
  currentPage = 1;
  pageSize = 10;
  totalPages = 1;
  isLoading = false;

  ngOnInit(): void {
    this.loadReports();
  }

  loadReports(): void {
    this.isLoading = true;
    this.reportService.getUserReports({
      status: this.selectedStatus,
      search: this.searchTerm,
      page: this.currentPage,
      pageSize: this.pageSize
    }).subscribe({
      next: (res: PagedReportResponse) => {
        this.reports = res.items;
        this.totalPages = res.totalPages;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  setStatus(status: string): void {
    this.selectedStatus = status;
    this.currentPage = 1;
    this.loadReports();
  }

  changePage(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.loadReports();
    }
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
