import { TranslatePipe } from '../core/pipes/translate.pipe';
import { Component, EventEmitter, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalyticsDateRange, AnalyticsDateRangeRequest } from '../core/models/analytics.models';

@Component({
  selector: 'app-analytics-date-filter',
  standalone: true,
  imports: [
    TranslatePipe,CommonModule, FormsModule],
  template: `
    <div class="flex items-center flex-wrap gap-3 bg-slate-900/90 border border-slate-800 p-2.5 rounded-2xl shadow-lg backdrop-blur-md">
      <div class="flex items-center gap-2">
        <span class="text-xs font-bold text-slate-400 uppercase tracking-wider pl-2 flex items-center gap-1.5">
          <svg class="w-4 h-4 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
          </svg>
          Date Range:
        </span>
        <select
          [ngModel]="selectedRange()"
          (ngModelChange)="onRangeChange($event)"
          class="bg-slate-950 text-white text-xs font-bold px-3 py-2 rounded-xl border border-slate-700 hover:border-slate-600 focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-pointer shadow-sm transition-all"
        >
          <option [value]="AnalyticsDateRange.Last30Days">Last 30 Days</option>
          <option [value]="AnalyticsDateRange.Today">Today</option>
          <option [value]="AnalyticsDateRange.Last7Days">Last 7 Days</option>
          <option [value]="AnalyticsDateRange.ThisMonth">This Month</option>
          <option [value]="AnalyticsDateRange.LastMonth">Last Month</option>
          <option [value]="AnalyticsDateRange.ThisYear">This Year</option>
          <option [value]="AnalyticsDateRange.Custom">Custom Date Range...</option>
        </select>
      </div>

      <!-- Custom Date Range Pickers -->
      <div *ngIf="selectedRange() === AnalyticsDateRange.Custom" class="flex items-center gap-2 flex-wrap">
        <div class="flex items-center gap-1.5">
          <span class="text-[10px] text-slate-400 font-bold uppercase">From:</span>
          <input
            type="date"
            [ngModel]="startDate()"
            (ngModelChange)="onCustomDateChange($event, endDate())"
            class="bg-slate-950 text-white text-xs px-2.5 py-1.5 rounded-lg border border-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
        <div class="flex items-center gap-1.5">
          <span class="text-[10px] text-slate-400 font-bold uppercase">To:</span>
          <input
            type="date"
            [ngModel]="endDate()"
            (ngModelChange)="onCustomDateChange(startDate(), $event)"
            class="bg-slate-950 text-white text-xs px-2.5 py-1.5 rounded-lg border border-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
      </div>
    </div>
  `
})
export class AnalyticsDateFilterComponent {
  @Output() dateRangeChange = new EventEmitter<AnalyticsDateRangeRequest>();

  readonly AnalyticsDateRange = AnalyticsDateRange;

  selectedRange = signal<AnalyticsDateRange>(AnalyticsDateRange.Last30Days);
  startDate = signal<string>('');
  endDate = signal<string>('');

  onRangeChange(range: AnalyticsDateRange): void {
    this.selectedRange.set(range);

    if (range !== AnalyticsDateRange.Custom) {
      this.dateRangeChange.emit({ range });
    } else {
      if (this.startDate() && this.endDate()) {
        this.emitCustomRange();
      }
    }
  }

  onCustomDateChange(start: string, end: string): void {
    this.startDate.set(start);
    this.endDate.set(end);

    if (start && end && start <= end) {
      this.emitCustomRange();
    }
  }

  private emitCustomRange(): void {
    this.dateRangeChange.emit({
      range: AnalyticsDateRange.Custom,
      customStartDateUtc: this.startDate() ? new Date(this.startDate()).toISOString() : null,
      customEndDateUtc: this.endDate() ? new Date(this.endDate()).toISOString() : null
    });
  }
}
