import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkerJobService } from './worker-job.service';
import { WorkerWorkHistoryItem, WorkerWorkHistorySummary } from '../../core/models/worker.models';

@Component({
  selector: 'app-worker-work-history',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './worker-work-history.component.html'
})
export class WorkerWorkHistoryComponent implements OnInit {
  private readonly workerService = inject(WorkerJobService);

  summary = signal<WorkerWorkHistorySummary | null>(null);
  filteredItems = signal<WorkerWorkHistoryItem[]>([]);
  loading = signal(true);
  loadError = signal<string | null>(null);

  searchQuery = signal<string>('');
  selectedFilter = signal<string>('All');

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.workerService.getWorkHistory().subscribe({
      next: (data) => {
        this.summary.set(data);
        this.applyFilter(data.historyItems);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set('Failed to load completed work history. Please try again.');
      }
    });
  }

  applyFilter(items?: WorkerWorkHistoryItem[]): void {
    const source = items || this.summary()?.historyItems || [];
    const q = this.searchQuery().toLowerCase().trim();
    const filter = this.selectedFilter();

    let result = source;

    if (filter === 'Rated') {
      result = result.filter(item => item.rating && item.rating > 0);
    } else if (filter === 'Unrated') {
      result = result.filter(item => !item.rating);
    }

    if (q) {
      result = result.filter(item =>
        item.jobTitle.toLowerCase().includes(q) ||
        item.farmerName.toLowerCase().includes(q) ||
        item.workCategory.toLowerCase().includes(q) ||
        item.location.toLowerCase().includes(q)
      );
    }

    this.filteredItems.set(result);
  }

  onSearchChange(val: string): void {
    this.searchQuery.set(val);
    this.applyFilter();
  }

  onFilterChange(filter: string): void {
    this.selectedFilter.set(filter);
    this.applyFilter();
  }
}
