import { TranslatePipe } from '../../core/pipes/translate.pipe';
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
    TranslatePipe,
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
        return 'app-badge--active';
      case 'Rejected':
        return 'app-badge--danger';
      case 'Withdrawn':
        return 'app-badge--soon';
      case 'Pending':
      default:
        return 'app-badge--pending';
    }
  }
}
