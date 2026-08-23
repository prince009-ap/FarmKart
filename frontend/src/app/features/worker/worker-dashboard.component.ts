import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { WorkerJobService } from './worker-job.service';
import {
  WorkerAssignment,
  WorkerAttendanceSummary,
  WorkerEarningsSummary,
  WorkerJobApplication,
  WorkerNotification,
  WorkerProfileCompletion
} from '../../core/models/worker.models';

import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { LanguageService } from '../../core/services/language.service';

@Component({
  selector: 'app-worker-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    TranslatePipe,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './worker-dashboard.component.html'
})
export class WorkerDashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly workerService = inject(WorkerJobService);
  readonly languageService = inject(LanguageService);

  userName = signal<string>('Worker');
  loading = signal<boolean>(true);

  applicationsCount = signal<number>(0);
  activeAssignmentsCount = signal<number>(0);
  monthlyEarnings = signal<number>(0);
  attendanceRate = signal<number>(0);
  profileCompletion = signal<WorkerProfileCompletion | null>(null);
  recentNotifications = signal<WorkerNotification[]>([]);

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userName.set(user.fullName || 'Worker');
      }
    });

    this.loadDashboardData();
  }

  loadDashboardData(): void {
    this.loading.set(true);

    // 1. Applications
    this.workerService.getMyApplications().subscribe({
      next: (apps) => this.applicationsCount.set(apps.length),
      error: () => {}
    });

    // 2. Assignments
    this.workerService.getMyAssignments().subscribe({
      next: (assigns) => {
        const active = assigns.filter(a => a.status === 'Active' || a.status === 'Pending').length;
        this.activeAssignmentsCount.set(active);
      },
      error: () => {}
    });

    // 3. Earnings
    this.workerService.getEarnings().subscribe({
      next: (earn) => this.monthlyEarnings.set(earn.thisMonthEarnings || earn.totalEarnings || 0),
      error: () => {}
    });

    // 4. Attendance Summary
    this.workerService.getMyAttendance().subscribe({
      next: (att) => this.attendanceRate.set(att.attendancePercentage || 0),
      error: () => {}
    });

    // 5. Profile Completion
    this.workerService.getProfileCompletion().subscribe({
      next: (comp) => this.profileCompletion.set(comp),
      error: () => {}
    });

    // 6. Recent Notifications
    this.workerService.getNotifications().subscribe({
      next: (notifs) => {
        this.recentNotifications.set(notifs.slice(0, 3));
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
