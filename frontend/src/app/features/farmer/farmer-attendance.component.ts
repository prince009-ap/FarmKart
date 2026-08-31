import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { AttendanceStatus, FarmerAttendanceRecord, FarmerJob, FarmerWorkerAssignment, MarkAttendanceItemRequest } from '../../core/models/farmer.models';
import { FarmerJobService } from './farmer-job.service';

interface AttendanceRow {
  assignment: FarmerWorkerAssignment;
  status: AttendanceStatus | '';
  notes: string;
}

@Component({
  selector: 'app-farmer-attendance',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: './farmer-attendance.component.html'
})
export class FarmerAttendanceComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly jobService = inject(FarmerJobService);

  job = signal<FarmerJob | null>(null);
  assignedWorkers = signal<FarmerWorkerAssignment[]>([]);
  attendanceRows = signal<AttendanceRow[]>([]);
  historyRecords = signal<FarmerAttendanceRecord[]>([]);

  selectedDate = signal<string>(new Date().toISOString().substring(0, 10));
  minDate = signal<string>('');
  maxDate = signal<string>('');
  isBeforeStartDate = signal<boolean>(false);

  loading = signal(true);
  saving = signal(false);
  actionMessage = signal('');
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

        const todayStr = new Date().toISOString().substring(0, 10);
        const start = job.startDate;
        const end = job.endDate || todayStr;

        this.minDate.set(start);
        const max = end < todayStr ? end : todayStr;
        this.maxDate.set(max);

        if (todayStr < start) {
          this.isBeforeStartDate.set(true);
          this.selectedDate.set(start);
        } else {
          this.isBeforeStartDate.set(false);
          this.selectedDate.set(max);
        }

        this.jobService.getJobAssignments(jobId).subscribe({
          next: workers => {
            this.assignedWorkers.set(workers);
            this.loadAttendanceForDate(jobId, this.selectedDate());
          },
          error: () => {
            this.error.set('Unable to load assigned workers for this job.');
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

  onDateChange(newDate: string): void {
    this.selectedDate.set(newDate);
    const currentJob = this.job();
    if (currentJob) {
      this.loadAttendanceForDate(currentJob.id, newDate);
    }
  }

  loadAttendanceForDate(jobId: string, date: string): void {
    this.loading.set(true);
    this.actionMessage.set('');
    this.error.set('');

    this.jobService.getJobAttendance(jobId, date).subscribe({
      next: records => {
        const recordMap = new Map<string, FarmerAttendanceRecord>();
        records.forEach(r => recordMap.set(r.workerAssignmentId, r));

        const rows: AttendanceRow[] = this.assignedWorkers().map(worker => {
          const rec = recordMap.get(worker.assignmentId);
          return {
            assignment: worker,
            status: rec ? rec.status : '',
            notes: rec?.notes || ''
          };
        });

        this.attendanceRows.set(rows);
        this.loadHistory(jobId);
      },
      error: () => {
        this.error.set('Unable to load attendance for the selected date.');
        this.loading.set(false);
      }
    });
  }

  loadHistory(jobId: string): void {
    this.jobService.getJobAttendance(jobId).subscribe({
      next: history => {
        this.historyRecords.set(history);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  get canSave(): boolean {
    if (this.isBeforeStartDate() || this.saving() || this.attendanceRows().length === 0) return false;
    return this.attendanceRows().every(r => r.status === 'Present' || r.status === 'Absent' || r.status === 'HalfDay' || r.status === 'Leave');
  }

  saveAttendance(): void {
    const currentJob = this.job();
    if (!currentJob) return;

    if (this.isBeforeStartDate()) {
      this.error.set(`Attendance will be available from ${this.minDate()}.`);
      return;
    }

    const unselected = this.attendanceRows().find(r => !r.status);
    if (unselected) {
      this.error.set('Please select an attendance status for all assigned workers before saving.');
      return;
    }

    this.saving.set(true);
    this.actionMessage.set('');
    this.error.set('');

    const items: MarkAttendanceItemRequest[] = this.attendanceRows().map(row => ({
      workerAssignmentId: row.assignment.assignmentId,
      status: row.status as AttendanceStatus,
      notes: row.notes || null
    }));

    this.jobService.saveJobAttendance(currentJob.id, {
      date: this.selectedDate(),
      items
    }).subscribe({
      next: updatedRecords => {
        this.saving.set(false);
        this.actionMessage.set(`Attendance for ${this.selectedDate()} saved successfully.`);
        this.loadHistory(currentJob.id);
      },
      error: err => {
        this.saving.set(false);
        this.error.set(err.error?.message || 'Failed to save attendance.');
      }
    });
  }

  setAllStatus(status: AttendanceStatus): void {
    this.attendanceRows.update(rows => rows.map(r => ({ ...r, status })));
  }
}
