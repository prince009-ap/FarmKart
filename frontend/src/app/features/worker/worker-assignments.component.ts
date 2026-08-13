import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkerAssignment } from '../../core/models/worker.models';
import { WorkerJobService } from './worker-job.service';

@Component({
  selector: 'app-worker-assignments',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './worker-assignments.component.html'
})
export class WorkerAssignmentsComponent implements OnInit {
  private readonly workerJobService = inject(WorkerJobService);

  assignments = signal<WorkerAssignment[]>([]);
  loading = signal(true);
  error = signal('');

  ngOnInit(): void {
    this.loadAssignments();
  }

  loadAssignments(): void {
    this.loading.set(true);
    this.error.set('');

    this.workerJobService.getMyAssignments().subscribe({
      next: list => {
        this.assignments.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load your job assignments.');
        this.loading.set(false);
      }
    });
  }
}
