import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkerJobApplication } from '../../core/models/worker.models';
import { WorkerJobService } from './worker-job.service';

@Component({
  selector: 'app-worker-applications',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './worker-applications.component.html'
})
export class WorkerApplicationsComponent implements OnInit {
  private readonly jobService = inject(WorkerJobService);

  applications = signal<WorkerJobApplication[]>([]);
  loading = signal(true);
  error = signal('');

  ngOnInit(): void {
    this.loadApplications();
  }

  loadApplications(): void {
    this.loading.set(true);
    this.error.set('');
    this.jobService.getMyApplications().subscribe({
      next: apps => {
        this.applications.set(apps);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load your applications. Please try again.');
        this.loading.set(false);
      }
    });
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Accepted':
        return 'bg-emerald-50 text-emerald-800 border-emerald-200';
      case 'Rejected':
        return 'bg-rose-50 text-rose-800 border-rose-200';
      case 'Withdrawn':
        return 'bg-gray-50 text-gray-700 border-gray-200';
      case 'Pending':
      default:
        return 'bg-amber-50 text-amber-800 border-amber-200';
    }
  }
}
