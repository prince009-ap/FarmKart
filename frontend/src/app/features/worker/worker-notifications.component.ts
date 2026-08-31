import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { WorkerJobService } from './worker-job.service';
import { WorkerNotification } from '../../core/models/worker.models';

@Component({
  selector: 'app-worker-notifications',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './worker-notifications.component.html'
})
export class WorkerNotificationsComponent implements OnInit {
  private readonly workerService = inject(WorkerJobService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  notifications = signal<WorkerNotification[]>([]);
  unreadCount = signal(0);
  loading = signal(false);
  markingAll = signal(false);
  loadError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.loadError.set(null);

    this.workerService.getNotifications().subscribe({
      next: (data) => {
        this.notifications.set(data);
        const count = data.filter(n => !n.isRead).length;
        this.unreadCount.set(count);
      },
      error: (err) => {
        this.loadError.set('Failed to load notifications. Please try again.');
      }
    });
  }

  markAsRead(notif: WorkerNotification): void {
    if (notif.isRead) return;

    this.workerService.markNotificationAsRead(notif.id).subscribe({
      next: (updated) => {
        this.notifications.update(list => list.map(n => n.id === updated.id ? updated : n));
        this.unreadCount.update(c => Math.max(0, c - 1));
      },
      error: (err) => {
        this.snackBar.open('Failed to mark notification as read.', 'Close', { duration: 3000 });
      }
    });
  }

  markAllAsRead(): void {
    if (this.unreadCount() === 0) return;

    this.markingAll.set(true);
    this.workerService.markAllNotificationsAsRead().subscribe({
      next: () => {
        this.notifications.update(list => list.map(n => ({ ...n, isRead: true })));
        this.unreadCount.set(0);
        this.markingAll.set(false);
        this.snackBar.open('All notifications marked as read.', 'Close', { duration: 3000 });
      },
      error: (err) => {
        this.markingAll.set(false);
        this.snackBar.open('Failed to mark all as read.', 'Close', { duration: 3000 });
      }
    });
  }

  navigateToNotificationTarget(notif: WorkerNotification): void {
    this.markAsRead(notif);

    if (notif.notificationType === 'Application' || notif.title.toLowerCase().includes('application')) {
      this.router.navigate(['/worker/applications']);
    } else if (notif.notificationType === 'Assignment' || notif.title.toLowerCase().includes('assignment')) {
      this.router.navigate(['/worker/assignments']);
    } else if (notif.title.toLowerCase().includes('attendance')) {
      this.router.navigate(['/worker/attendance']);
    } else if (notif.notificationType === 'Job' || notif.title.toLowerCase().includes('job')) {
      this.router.navigate(['/worker/jobs']);
    }
  }

  getNotificationIcon(type: string, title: string): string {
    const t = (title || '').toLowerCase();
    if (t.includes('accept')) return 'check_circle';
    if (t.includes('reject')) return 'cancel';
    if (t.includes('assign')) return 'assignment_ind';
    if (t.includes('attendance')) return 'event_available';
    if (t.includes('cancel')) return 'event_busy';
    return 'notifications';
  }
}
