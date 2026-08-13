import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkerAssignment } from '../../core/models/worker.models';
import { WorkerJobService } from './worker-job.service';

@Component({
  selector: 'app-worker-assignment-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './worker-assignment-detail.component.html'
})
export class WorkerAssignmentDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly workerJobService = inject(WorkerJobService);

  assignment = signal<WorkerAssignment | null>(null);
  loading = signal(true);
  error = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadAssignment(id);
    } else {
      this.error.set('Invalid assignment identifier.');
      this.loading.set(false);
    }
  }

  loadAssignment(id: string): void {
    this.loading.set(true);
    this.error.set('');

    this.workerJobService.getAssignmentDetails(id).subscribe({
      next: item => {
        this.assignment.set(item);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Assignment not found.');
        this.loading.set(false);
      }
    });
  }
}
