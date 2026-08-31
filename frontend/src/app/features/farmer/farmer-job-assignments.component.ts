import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FarmerJob, FarmerWorkerAssignment } from '../../core/models/farmer.models';
import { CreateWorkerReviewRequest, WorkerReview } from '../../core/models/worker.models';
import { FarmerJobService } from './farmer-job.service';

@Component({
  selector: 'app-farmer-job-assignments',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './farmer-job-assignments.component.html'
})
export class FarmerJobAssignmentsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly jobService = inject(FarmerJobService);
  private readonly snackBar = inject(MatSnackBar);

  job = signal<FarmerJob | null>(null);
  assignments = signal<FarmerWorkerAssignment[]>([]);
  loading = signal(true);
  error = signal('');

  // Rating Modal state
  selectedAssignmentForRating = signal<FarmerWorkerAssignment | null>(null);
  ratingValue = signal<number>(5);
  reviewComment = signal<string>('');
  submittingRating = signal<boolean>(false);
  ratingError = signal<string | null>(null);
  existingReviewsMap = signal<Record<string, WorkerReview>>({});

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
            this.loadExistingReviews(list);
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

  private loadExistingReviews(list: FarmerWorkerAssignment[]): void {
    const map: Record<string, WorkerReview> = {};
    list.forEach(assignment => {
      this.jobService.getWorkerReview(assignment.assignmentId).subscribe({
        next: (review) => {
          if (review) {
            map[assignment.assignmentId] = review;
            this.existingReviewsMap.set({ ...map });
          }
        },
        error: () => {}
      });
    });
  }

  isEligibleForRating(assignment: FarmerWorkerAssignment): boolean {
    const today = new Date().toISOString().split('T')[0];
    const job = this.job();
    const isCompleted = assignment.status === 'Completed' || (job && job.status === 'Completed');
    const isEndDatePassed = Boolean((assignment.endDate && assignment.endDate <= today) || (job && job.endDate && job.endDate <= today));
    return Boolean(isCompleted || isEndDatePassed);
  }

  openRateModal(assignment: FarmerWorkerAssignment): void {
    this.selectedAssignmentForRating.set(assignment);
    this.ratingError.set(null);

    const existing = this.existingReviewsMap()[assignment.assignmentId];
    if (existing) {
      this.ratingValue.set(existing.rating);
      this.reviewComment.set(existing.comment || '');
    } else {
      this.ratingValue.set(5);
      this.reviewComment.set('');
    }
  }

  closeRateModal(): void {
    this.selectedAssignmentForRating.set(null);
    this.submittingRating.set(false);
    this.ratingError.set(null);
  }

  setRating(stars: number): void {
    this.ratingValue.set(stars);
  }

  submitRating(): void {
    const assignment = this.selectedAssignmentForRating();
    if (!assignment) return;

    if (this.ratingValue() < 1 || this.ratingValue() > 5) {
      this.ratingError.set('Rating must be between 1 and 5 stars.');
      return;
    }

    if (this.reviewComment().length > 2000) {
      this.ratingError.set('Review comment cannot exceed 2000 characters.');
      return;
    }

    this.submittingRating.set(true);
    this.ratingError.set(null);

    const req: CreateWorkerReviewRequest = {
      rating: this.ratingValue(),
      comment: this.reviewComment().trim() || null
    };

    this.jobService.rateWorker(assignment.assignmentId, req).subscribe({
      next: (review) => {
        this.existingReviewsMap.update(map => ({ ...map, [assignment.assignmentId]: review }));
        this.submittingRating.set(false);
        this.snackBar.open(`Worker ${assignment.workerName} rated ${review.rating} stars!`, 'Close', { duration: 4000 });
        this.closeRateModal();
      },
      error: (err) => {
        this.submittingRating.set(false);
        const msg = err.error?.message || 'Failed to submit worker rating. Please try again.';
        this.ratingError.set(msg);
      }
    });
  }
}
