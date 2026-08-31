import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkerJobService } from './worker-job.service';
import { WorkerEarningsSummary } from '../../core/models/worker.models';

@Component({
  selector: 'app-worker-earnings',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './worker-earnings.component.html'
})
export class WorkerEarningsComponent implements OnInit {
  private readonly workerService = inject(WorkerJobService);

  summary = signal<WorkerEarningsSummary | null>(null);
  loading = signal(true);
  loadError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadEarnings();
  }

  loadEarnings(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.workerService.getEarnings().subscribe({
      next: (data) => {
        this.summary.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.loadError.set('Failed to load earnings history. Please try again.');
      }
    });
  }
}
