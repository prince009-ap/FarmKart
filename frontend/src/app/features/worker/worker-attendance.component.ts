import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkerAttendanceSummary } from '../../core/models/worker.models';
import { WorkerJobService } from './worker-job.service';

@Component({
  selector: 'app-worker-attendance',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './worker-attendance.component.html'
})
export class WorkerAttendanceComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly workerJobService = inject(WorkerJobService);

  summary = signal<WorkerAttendanceSummary | null>(null);
  loading = signal(true);
  error = signal('');
  assignmentId = signal<string | null>(null);

  ngOnInit(): void {
    const aid = this.route.snapshot.paramMap.get('assignmentId');
    if (aid) {
      this.assignmentId.set(aid);
      this.loadAssignmentAttendance(aid);
    } else {
      this.loadAllAttendance();
    }
  }

  loadAllAttendance(): void {
    this.loading.set(true);
    this.error.set('');

    this.workerJobService.getMyAttendance().subscribe({
      next: data => {
        this.summary.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load attendance records.');
        this.loading.set(false);
      }
    });
  }

  loadAssignmentAttendance(id: string): void {
    this.loading.set(true);
    this.error.set('');

    this.workerJobService.getAssignmentAttendance(id).subscribe({
      next: data => {
        this.summary.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load attendance for this assignment.');
        this.loading.set(false);
      }
    });
  }
}
