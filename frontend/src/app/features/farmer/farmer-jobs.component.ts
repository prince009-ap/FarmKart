import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FarmerJob } from '../../core/models/farmer.models';
import { FarmerJobService } from './farmer-job.service';
import { ConfirmDialogService } from '../../shared/dialogs/confirm-dialog.service';

@Component({
  selector: 'app-farmer-jobs',
  standalone: true,
  imports: [
    TranslatePipe,CommonModule, RouterLink, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './farmer-jobs.component.html'
})
export class FarmerJobsComponent implements OnInit {
  private readonly jobService = inject(FarmerJobService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  jobs = signal<FarmerJob[]>([]);
  loading = signal(true);
  error = signal('');
  cancellingId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadJobs();
  }

  loadJobs(): void {
    this.loading.set(true);
    this.error.set('');
    this.jobService.getMyJobs().subscribe({
      next: jobs => {
        this.jobs.set(jobs);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load your jobs. Please try again.');
        this.loading.set(false);
      }
    });
  }

  cancelJob(job: FarmerJob): void {
    this.confirmDialog.confirm({
      title: 'Cancel job posting',
      message: `Cancel "${job.title}"? Workers will no longer be able to apply.`,
      confirmLabel: 'Cancel job',
      tone: 'danger',
      icon: 'delete_outline'
    }).subscribe(confirmed => {
      if (!confirmed) return;

      this.cancellingId.set(job.id);
      this.jobService.deleteJob(job.id).subscribe({
        next: () => {
          this.cancellingId.set(null);
          this.loadJobs();
        },
        error: () => {
          this.error.set('Unable to cancel this job.');
          this.cancellingId.set(null);
        }
      });
    });
  }

  canEdit(job: FarmerJob): boolean {
    return job.status === 'Draft' || job.status === 'Open';
  }
}
