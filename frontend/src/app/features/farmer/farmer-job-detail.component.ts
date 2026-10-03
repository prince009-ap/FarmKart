import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FarmerJob, WorkerRecommendationResponse } from '../../core/models/farmer.models';
import { FarmerJobService } from './farmer-job.service';

@Component({
  selector: 'app-farmer-job-detail',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './farmer-job-detail.component.html'
})
export class FarmerJobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly jobService = inject(FarmerJobService);

  job = signal<FarmerJob | null>(null);
  loading = signal(true);
  error = signal('');

  // Recommendation signals
  recommendations = signal<WorkerRecommendationResponse | null>(null);
  recommendationsLoading = signal(false);
  recommendationsError = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadJob(id);
  }

  loadJob(id: string): void {
    this.loading.set(true);
    this.error.set('');
    this.jobService.getJob(id).subscribe({
      next: job => {
        this.job.set(job);
        this.loading.set(false);
        this.loadRecommendations(job.id);
      },
      error: () => {
        this.error.set('Job not found.');
        this.loading.set(false);
      }
    });
  }

  loadRecommendations(jobId: string): void {
    this.recommendationsLoading.set(true);
    this.recommendationsError.set('');
    this.jobService.getRecommendedWorkers(jobId, 5).subscribe({
      next: res => {
        this.recommendations.set(res);
        this.recommendationsLoading.set(false);
      },
      error: () => {
        this.recommendationsError.set('Unable to load AI worker recommendations at this time.');
        this.recommendationsLoading.set(false);
      }
    });
  }
}
