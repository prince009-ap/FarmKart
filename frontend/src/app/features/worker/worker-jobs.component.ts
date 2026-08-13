import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { WorkerAvailableJob } from '../../core/models/worker.models';
import { WorkerJobService } from './worker-job.service';

@Component({
  selector: 'app-worker-jobs',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule
  ],
  templateUrl: './worker-jobs.component.html'
})
export class WorkerJobsComponent implements OnInit {
  private readonly jobService = inject(WorkerJobService);

  jobs = signal<WorkerAvailableJob[]>([]);
  loading = signal(true);
  error = signal('');

  searchTerm = signal('');
  selectedCategory = signal('');
  selectedLocation = signal('');

  categories = computed(() => {
    const list = this.jobs().map(j => j.workCategory);
    return Array.from(new Set(list)).sort();
  });

  filteredJobs = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const cat = this.selectedCategory();
    const loc = this.selectedLocation().toLowerCase().trim();

    return this.jobs().filter(job => {
      const matchesTerm = !term ||
        job.title.toLowerCase().includes(term) ||
        (job.cropType && job.cropType.toLowerCase().includes(term)) ||
        job.description.toLowerCase().includes(term);

      const matchesCat = !cat || job.workCategory === cat;
      const matchesLoc = !loc || job.farmLocation.toLowerCase().includes(loc);

      return matchesTerm && matchesCat && matchesLoc;
    });
  });

  ngOnInit(): void {
    this.loadJobs();
  }

  loadJobs(): void {
    this.loading.set(true);
    this.error.set('');
    this.jobService.getAvailableJobs().subscribe({
      next: jobs => {
        this.jobs.set(jobs);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load available jobs. Please try again.');
        this.loading.set(false);
      }
    });
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.selectedCategory.set('');
    this.selectedLocation.set('');
  }
}
