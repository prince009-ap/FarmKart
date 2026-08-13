import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FarmerJob, FarmerJobApplication } from '../../core/models/farmer.models';
import { FarmerJobService } from './farmer-job.service';
import { ConfirmDialogService } from '../../shared/dialogs/confirm-dialog.service';

@Component({
  selector: 'app-farmer-job-applications',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './farmer-job-applications.component.html'
})
export class FarmerJobApplicationsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly jobService = inject(FarmerJobService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  job = signal<FarmerJob | null>(null);
  applications = signal<FarmerJobApplication[]>([]);
  loading = signal(true);
  error = signal('');
  processingId = signal<string | null>(null);
  actionMessage = signal('');
  actionErrorMessage = signal('');

  acceptedCount = computed(() =>
    this.applications().filter(a => a.status === 'Accepted').length
  );

  pendingCount = computed(() =>
    this.applications().filter(a => a.status === 'Pending').length
  );

  capacityReached = computed(() => {
    const currentJob = this.job();
    if (!currentJob) return false;
    return this.acceptedCount() >= currentJob.workersRequired;
  });

  ngOnInit(): void {
    const jobId = this.route.snapshot.paramMap.get('jobId');
    if (jobId) {
      this.loadData(jobId);
    } else {
      this.error.set('Invalid job identifier.');
      this.loading.set(false);
    }
  }

  loadData(jobId: string): void {
    this.loading.set(true);
    this.error.set('');
    this.actionMessage.set('');
    this.actionErrorMessage.set('');

    this.jobService.getJob(jobId).subscribe({
      next: job => {
        this.job.set(job);
        this.jobService.getJobApplications(jobId).subscribe({
          next: apps => {
            this.applications.set(apps);
            this.loading.set(false);
          },
          error: () => {
            this.error.set('Unable to load applications for this job.');
            this.loading.set(false);
          }
        });
      },
      error: () => {
        this.error.set('Job not found.');
        this.loading.set(false);
      }
    });
  }

  acceptApplication(app: FarmerJobApplication): void {
    this.confirmDialog.confirm({
      title: 'Accept application',
      message: `Accept ${app.applicantName} for this job? They will count toward your hiring capacity.`,
      confirmLabel: 'Accept',
      tone: 'success',
      icon: 'check_circle'
    }).subscribe(confirmed => {
      if (!confirmed) return;

      this.processingId.set(app.applicationId);
      this.actionMessage.set('');
      this.actionErrorMessage.set('');

      this.jobService.acceptApplication(app.applicationId).subscribe({
        next: updatedApp => {
          this.processingId.set(null);
          this.actionMessage.set(`Successfully accepted ${app.applicantName}'s application.`);
          this.updateLocalAppStatus(updatedApp);
        },
        error: err => {
          this.processingId.set(null);
          this.actionErrorMessage.set(err.error?.message || 'Failed to accept application.');
        }
      });
    });
  }

  rejectApplication(app: FarmerJobApplication): void {
    this.confirmDialog.confirm({
      title: 'Reject application',
      message: `Reject ${app.applicantName}'s application? This action cannot be undone.`,
      confirmLabel: 'Reject',
      tone: 'danger',
      icon: 'cancel'
    }).subscribe(confirmed => {
      if (!confirmed) return;

      this.processingId.set(app.applicationId);
      this.actionMessage.set('');
      this.actionErrorMessage.set('');

      this.jobService.rejectApplication(app.applicationId).subscribe({
        next: updatedApp => {
          this.processingId.set(null);
          this.actionMessage.set(`Rejected ${app.applicantName}'s application.`);
          this.updateLocalAppStatus(updatedApp);
        },
        error: err => {
          this.processingId.set(null);
          this.actionErrorMessage.set(err.error?.message || 'Failed to reject application.');
        }
      });
    });
  }

  statusClass(status: FarmerJobApplication['status']): string {
    switch (status) {
      case 'Accepted':
        return 'app-badge--active';
      case 'Rejected':
        return 'app-badge--danger';
      default:
        return 'app-badge--pending';
    }
  }

  private updateLocalAppStatus(updatedApp: FarmerJobApplication): void {
    this.applications.update(list =>
      list.map(a =>
        a.applicationId === updatedApp.applicationId ? { ...a, status: updatedApp.status } : a
      )
    );
  }
}
