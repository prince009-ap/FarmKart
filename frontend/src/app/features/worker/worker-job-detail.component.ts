import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { WorkerAvailableJob } from '../../core/models/worker.models';
import { WorkerJobService } from './worker-job.service';

@Component({
  selector: 'app-worker-job-detail',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule
  ],
  templateUrl: './worker-job-detail.component.html'
})
export class WorkerJobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly jobService = inject(WorkerJobService);

  job = signal<WorkerAvailableJob | null>(null);
  loading = signal(true);
  error = signal('');

  applicationMessage = signal('');
  submitting = signal(false);
  submitSuccess = signal(false);
  submitError = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadJob(id);
    } else {
      this.error.set('Invalid job identifier.');
      this.loading.set(false);
    }
  }

  loadJob(id: string): void {
    this.loading.set(true);
    this.error.set('');
    this.jobService.getJobDetails(id).subscribe({
      next: job => {
        this.job.set(job);
        this.loading.set(false);
      },
      error: err => {
        if (err.status === 404) {
          this.error.set('This job is no longer available.');
        } else {
          this.error.set('Unable to load job details. Please try again.');
        }
        this.loading.set(false);
      }
    });
  }

  applyNow(): void {
    const currentJob = this.job();
    if (!currentJob || currentJob.hasApplied || this.submitting()) return;

    this.submitting.set(true);
    this.submitError.set('');

    this.jobService.applyToJob(currentJob.id, { message: this.applicationMessage() }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitSuccess.set(true);
        this.job.set({ ...currentJob, hasApplied: true });
      },
      error: err => {
        this.submitting.set(false);
        if (err.status === 409) {
          this.submitError.set('You have already applied to this job.');
          this.job.set({ ...currentJob, hasApplied: true });
        } else {
          this.submitError.set(err.error?.message || 'Failed to submit application. Please try again.');
        }
      }
    });
  }
}
