import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FarmerJob, FarmerWorkerAssignment } from '../../core/models/farmer.models';
import { FarmerJobService } from './farmer-job.service';

@Component({
  selector: 'app-farmer-job-assignments',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './farmer-job-assignments.component.html'
})
export class FarmerJobAssignmentsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly jobService = inject(FarmerJobService);

  job = signal<FarmerJob | null>(null);
  assignments = signal<FarmerWorkerAssignment[]>([]);
  loading = signal(true);
  error = signal('');

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

    this.jobService.getJob(jobId).subscribe({
      next: job => {
        this.job.set(job);
        this.jobService.getJobAssignments(jobId).subscribe({
          next: list => {
            this.assignments.set(list);
            this.loading.set(false);
          },
          error: () => {
            this.error.set('Unable to load assignments for this job.');
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
}
